/*using System;
using System.Collections.Generic;
using Godot;

public class AgenteIA
{
    private Tablero tableroMundo;
    public Coordenada PosicionActual { get; private set; } = new Coordenada(0, 0);
    public HashSet<Coordenada> Visitados { get; private set; } = new HashSet<Coordenada>();
    public Dictionary<Coordenada, EstadoInferencia> BaseConocimiento { get; private set; } = new Dictionary<Coordenada, EstadoInferencia>();
    public List<Coordenada> HistorialCamino { get; private set; } = new List<Coordenada>();

    // MATRICES MENTALES DE SOSPECHAS (Verdadera Lógica Proposicional)
    private HashSet<Coordenada> SospechasHoyo = new HashSet<Coordenada>();
    private HashSet<Coordenada> SospechasWumpus = new HashSet<Coordenada>();
    private HashSet<Coordenada> DescartesHoyo = new HashSet<Coordenada>();
    private HashSet<Coordenada> DescartesWumpus = new HashSet<Coordenada>();

    private bool tieneOro = false;
    private bool tieneFlecha = true;
    public bool JuegoTerminado { get; private set; } = false;
    // Diccionario histórico para recordar qué sensores había en cada casilla visitada
    private Dictionary<Coordenada, Percepcion> HistorialSensores = new Dictionary<Coordenada, Percepcion>();

    public AgenteIA(Tablero mundo)
    {
        tableroMundo = mundo;
        BaseConocimiento[PosicionActual] = EstadoInferencia.Seguro;
        HistorialCamino.Add(PosicionActual);
        Visitados.Add(PosicionActual);
    }

    public void EjecutarTurno()
    {
        if (JuegoTerminado) return;

        Percepcion sensores = tableroMundo.Matriz[PosicionActual.X, PosicionActual.Y].Sensores;
        Elemento contenidoReal = tableroMundo.Matriz[PosicionActual.X, PosicionActual.Y].Contenido;

        if ((sensores & Percepcion.Grito) != 0)
        {
            GD.Print("[IA] 🔊 Escuché el Grito del Wumpus. ¡El peligro del monstruo ha desaparecido!");
            LimpiarHedoresDelHistorial();
            tableroMundo.Matriz[PosicionActual.X, PosicionActual.Y].Sensores &= ~Percepcion.Grito;
        }

        GD.Print($"\n[IA] Ubicado en {PosicionActual}. Sensores detectados: {sensores}");

        if (contenidoReal == Elemento.Oro && !tieneOro)
        {
            GD.Print("[IA] 💰 ¡¡¡ORO ENCONTRADO!!! Iniciando retorno seguro.");
            tieneOro = true;
        }
        if (contenidoReal == Elemento.Hoyo || contenidoReal == Elemento.Wumpus)
        {
            GD.Print($"[IA] 💀 El agente murió en {PosicionActual} por un {contenidoReal}.");
            JuegoTerminado = true;
            return;
        }
        if (tieneOro && PosicionActual.X == 0 && PosicionActual.Y == 0)
        {
            GD.Print("[IA] 🎉 ¡VICTORIA! Regreso exitoso a la entrada.");
            JuegoTerminado = true;
            return;
        }

        // 1. Ejecutar inferencia lógica estricta
        ActualizarBaseConocimiento(sensores);

        // 2. Decidir y moverse
        Coordenada siguienteStep = DecidirProximaCelda();

        if (!siguienteStep.Equals(PosicionActual))
        {
            if (HistorialCamino.Count > 1 && siguienteStep.Equals(HistorialCamino[HistorialCamino.Count - 2]))
            {
                HistorialCamino.RemoveAt(HistorialCamino.Count - 1);
                PosicionActual = siguienteStep;
            }
            else
            {
                PosicionActual = siguienteStep;
                Visitados.Add(PosicionActual);
                HistorialCamino.Add(PosicionActual);
            }
        }
        else
        {
            GD.Print("[IA] 🛑 Sin salidas posibles en toda la cueva. Bloqueado.");
            JuegoTerminado = true;
        }
    }

    private void LimpiarHedoresDelHistorial()
    {
        var llaves = new List<Coordenada>(HistorialSensores.Keys);
        foreach (var coord in llaves)
        {
            // Quitar el bit de Hedor de todas las entradas históricas
            HistorialSensores[coord] = HistorialSensores[coord] & ~Percepcion.Hedor;
        }
    }

    private void ActualizarBaseConocimiento(Percepcion sensoresActuales)
    {
        HistorialSensores[PosicionActual] = sensoresActuales;

        SospechasHoyo.Clear();
        SospechasWumpus.Clear();
        DescartesHoyo.Clear();
        DescartesWumpus.Clear();

        foreach (var v in Visitados)
        {
            DescartesHoyo.Add(v);
            DescartesWumpus.Add(v);
        }

        foreach (var hecho in HistorialSensores)
        {
            Coordenada c = hecho.Key;
            Percepcion p = hecho.Value;
            List<Coordenada> adyacentesC = ObtenerAdyacentes(c);

            if ((p & Percepcion.Brisa) == 0)
            {
                foreach (var v in adyacentesC) DescartesHoyo.Add(v);
            }
            else
            {
                foreach (var v in adyacentesC) if (!Visitados.Contains(v)) SospechasHoyo.Add(v);
            }

            if ((p & Percepcion.Hedor) == 0)
            {
                foreach (var v in adyacentesC) DescartesWumpus.Add(v);
            }
            else
            {
                foreach (var v in adyacentesC) if (!Visitados.Contains(v)) SospechasWumpus.Add(v);
            }
        }

        // =========================================================================
        // INFERENCIA POR ELIMINACIÓN
        // Si todas las sospechas de una fuente menos una han sido descartadas,
        // la restante es CONFIRMADA.
        // =========================================================================
        var hoyo_confirmado = new HashSet<Coordenada>();
        var wumpus_confirmado = new HashSet<Coordenada>();

        // Para cada celda con brisa en el historial, ver si sus vecinos sospechosos
        // se redujeron a uno solo — ese uno es hoyo confirmado
        foreach (var hecho in HistorialSensores)
        {
            Coordenada c = hecho.Key;
            Percepcion p = hecho.Value;
            if ((p & Percepcion.Brisa) == 0) continue;

            List<Coordenada> candidatos = new List<Coordenada>();
            foreach (var v in ObtenerAdyacentes(c))
            {
                if (!DescartesHoyo.Contains(v)) candidatos.Add(v);
            }
            if (candidatos.Count == 1) hoyo_confirmado.Add(candidatos[0]);
        }

        foreach (var hecho in HistorialSensores)
        {
            Coordenada c = hecho.Key;
            Percepcion p = hecho.Value;
            if ((p & Percepcion.Hedor) == 0) continue;

            List<Coordenada> candidatos = new List<Coordenada>();
            foreach (var v in ObtenerAdyacentes(c))
            {
                if (!DescartesWumpus.Contains(v)) candidatos.Add(v);
            }
            if (candidatos.Count == 1) wumpus_confirmado.Add(candidatos[0]);
        }

        int d = tableroMundo.Dimension;
        for (int x = 0; x < d; x++)
        {
            for (int y = 0; y < d; y++)
            {
                Coordenada v = new Coordenada(x, y);
                if (Visitados.Contains(v)) continue;

                bool esHoyoDesc    = DescartesHoyo.Contains(v);
                bool esWumpusDesc  = DescartesWumpus.Contains(v) || !tableroMundo.WumpusVivo;
                bool esHoyoConf    = hoyo_confirmado.Contains(v);
                bool esWumpusConf  = wumpus_confirmado.Contains(v);

                // Confirmados tienen máxima prioridad
                if (esHoyoConf)
                {
                    BaseConocimiento[v] = EstadoInferencia.HoyoConfirmado;
                    GD.Print($"[IA] 🧠 Inferencia: {v} es HOYO CONFIRMADO por eliminación.");
                }
                else if (esWumpusConf && !esWumpusDesc)
                {
                    BaseConocimiento[v] = EstadoInferencia.WumpusConfirmado;
                    GD.Print($"[IA] 🧠 Inferencia: {v} es WUMPUS CONFIRMADO por eliminación.");
                }
                else if (esHoyoDesc && esWumpusDesc)
                    BaseConocimiento[v] = EstadoInferencia.Seguro;
                else if (!esWumpusDesc && SospechasWumpus.Contains(v))
                    BaseConocimiento[v] = EstadoInferencia.PosibleWumpus;
                else if (!esHoyoDesc && SospechasHoyo.Contains(v))
                    BaseConocimiento[v] = EstadoInferencia.PosibleHoyo;
                else
                    BaseConocimiento[v] = EstadoInferencia.Desconocido;
            }
        }
    }

    private Coordenada DecidirProximaCelda()
    {
        List<Coordenada> opcionesAdyacentes = ObtenerAdyacentes(PosicionActual);

        if (tieneOro)
        {
            int idxActual = HistorialCamino.LastIndexOf(PosicionActual);
            if (idxActual > 0) return HistorialCamino[idxActual - 1];
        }

        // PRIORIDAD 1: Avanzar a celda contigua que sea 100% SEGURA y NO visitada
        foreach (var opc in opcionesAdyacentes)
        {
            if (!Visitados.Contains(opc) && BaseConocimiento.ContainsKey(opc) && BaseConocimiento[opc] == EstadoInferencia.Seguro)
            {
                return opc;
            }
        }

        // PRIORIDAD 2: Backtracking inteligente si hay zonas seguras sin visitar en el mapa
        bool hayZonasSegurasPendientes = false;
        foreach (var nodo in BaseConocimiento)
        {
            if (nodo.Value == EstadoInferencia.Seguro && !Visitados.Contains(nodo.Key))
            {
                hayZonasSegurasPendientes = true;
                break;
            }
        }

        if (hayZonasSegurasPendientes && HistorialCamino.Count > 1)
        {
            GD.Print("[IA] ↩️ Retrocediendo para buscar otras ramas seguras del mapa...");
            return HistorialCamino[HistorialCamino.Count - 2];
        }

        // PRIORIDAD 3: Usar flecha si hay Wumpus sospechoso y estamos adyacentes a él
        // (o si el historial indica hedor desde nuestra posición actual)
        if (tieneFlecha && tableroMundo.WumpusVivo)
        {
            bool encontroObjetivo = false;
            Coordenada objetivoWumpus = default;

            foreach (var nodo in BaseConocimiento)
            {
                if (nodo.Value == EstadoInferencia.WumpusConfirmado && !Visitados.Contains(nodo.Key))
                {
                    objetivoWumpus = nodo.Key;
                    encontroObjetivo = true;
                    break;
                }
            }
            if (!encontroObjetivo)
            {
                foreach (var nodo in BaseConocimiento)
                {
                    if (nodo.Value == EstadoInferencia.PosibleWumpus && !Visitados.Contains(nodo.Key))
                    {
                        objetivoWumpus = nodo.Key;
                        encontroObjetivo = true;
                        break;
                    }
                }
            }

            if (encontroObjetivo)
            {
                List<Coordenada> adyacentesObjetivo = ObtenerAdyacentes(objetivoWumpus);
                bool estamosAdyacentes = adyacentesObjetivo.Contains(PosicionActual);

                bool objetivoEsValido = objetivoWumpus.X >= 0 && objetivoWumpus.X < tableroMundo.Dimension &&
                    objetivoWumpus.Y >= 0 && objetivoWumpus.Y < tableroMundo.Dimension;

                if (estamosAdyacentes && objetivoEsValido)
                {
                    Coordenada dir = new Coordenada(
                        objetivoWumpus.X - PosicionActual.X,
                        objetivoWumpus.Y - PosicionActual.Y
                    );
                    GD.Print($"[IA] 🏹 Disparando flecha hacia {objetivoWumpus}...");
                    tieneFlecha = false;
                    bool golpeo = tableroMundo.RegistrarDisparoFlecha(PosicionActual, dir);

                    if (golpeo)
                    {
                        GD.Print("[IA] 🎯 ¡Wumpus eliminado! Recalculando mapa...");
                        LimpiarHedoresDelHistorial();
                    }
                    else
                    {
                        GD.Print($"[IA] 🧠 Fallo confirmado. {objetivoWumpus} descartada como peligro.");
                    }

                    BaseConocimiento[objetivoWumpus] = EstadoInferencia.Seguro;
                    ActualizarBaseConocimiento(
                        tableroMundo.Matriz[PosicionActual.X, PosicionActual.Y].Sensores
                    );
                    return DecidirProximaCelda();
                }
                else
                {
                    if (HistorialCamino.Count > 1)
                    {
                        GD.Print("[IA] ↩️ Aproximándome al Wumpus para usar la flecha...");
                        return HistorialCamino[HistorialCamino.Count - 2];
                    }
                }
            }
        }

        // PRIORIDAD 4: Riesgo — NUNCA ir a celda confirmada como peligrosa
        foreach (var opc in opcionesAdyacentes)
        {
            if (!Visitados.Contains(opc) &&
                BaseConocimiento.ContainsKey(opc) &&
                BaseConocimiento[opc] != EstadoInferencia.HoyoConfirmado &&
                BaseConocimiento[opc] != EstadoInferencia.WumpusConfirmado)
            {
                GD.Print($"[IA] 🎲 Riesgo calculado hacia: {opc}");
                return opc;
            }
        }

        if (HistorialCamino.Count > 1) return HistorialCamino[HistorialCamino.Count - 2];
        return PosicionActual;
    }

    private List<Coordenada> ObtenerAdyacentes(Coordenada c)
    {
        List<Coordenada> lista = new List<Coordenada>();
        int d = tableroMundo.Dimension;
        if (c.X + 1 < d)  lista.Add(new Coordenada(c.X + 1, c.Y));
        if (c.X - 1 >= 0) lista.Add(new Coordenada(c.X - 1, c.Y));
        if (c.Y + 1 < d)  lista.Add(new Coordenada(c.X, c.Y + 1));
        if (c.Y - 1 >= 0) lista.Add(new Coordenada(c.X, c.Y - 1));
        return lista;
    }

    // Metodos de acceso publico
    public bool ObtenerEstadoTieneOro()
    {
        return tieneOro;
    }

    public bool ObtenerEstadoTieneFlecha()
    {
        return tieneFlecha;
    }
}*/

using System;
using System.Collections.Generic;
using Godot;

// =============================================================================
// AgenteIA.cs — Agente Inteligente para el Mundo de Wumpus
// =============================================================================
// LÓGICA PROPOSICIONAL implementada (reglas de inferencia activas):
//
//   R1: ¬Brisa(x,y)  → ∀ adyacente(a, x,y): ¬Hoyo(a)          (descartar hoyo)
//   R2:  Brisa(x,y)  → ∃ adyacente(a, x,y):  Hoyo(a)           (sospechar hoyo)
//   R3: ¬Hedor(x,y)  → ∀ adyacente(a, x,y): ¬Wumpus(a)         (descartar wumpus)
//   R4:  Hedor(x,y)  → ∃ adyacente(a, x,y):  Wumpus(a)         (sospechar wumpus)
//   R5: Visitado(x,y) → Seguro(x,y)                             (si pasé, es seguro)
//   R6: Candidatos_Brisa(c) = 1 → HoyoConfirmado(c)            (eliminación)
//   R7: Candidatos_Hedor(c) = 1 → WumpusConfirmado(c)          (eliminación)
//
// BÚSQUEDA NO INFORMADA:
//   BFS — BuscarRutaBFS(origen, destino) — usado para retorno seguro con el oro
//
// BÚSQUEDA INFORMADA:
//   A* — BuscarRutaAEstrella(origen, destino) — usado para navegar hacia la
//        celda segura no visitada más cercana durante la exploración
// =============================================================================

public class AgenteIA
{
    private Tablero tableroMundo;
    public Coordenada PosicionActual { get; private set; } = new Coordenada(0, 0);
    public HashSet<Coordenada> Visitados { get; private set; } = new HashSet<Coordenada>();
    public Dictionary<Coordenada, EstadoInferencia> BaseConocimiento { get; private set; } = new Dictionary<Coordenada, EstadoInferencia>();
    public List<Coordenada> HistorialCamino { get; private set; } = new List<Coordenada>();

    // -------------------------------------------------------------------------
    // MATRICES MENTALES DE SOSPECHAS (Lógica Proposicional — Reglas R1-R4)
    // -------------------------------------------------------------------------
    private HashSet<Coordenada> SospechasHoyo   = new HashSet<Coordenada>();
    private HashSet<Coordenada> SospechasWumpus  = new HashSet<Coordenada>();
    private HashSet<Coordenada> DescartesHoyo    = new HashSet<Coordenada>();
    private HashSet<Coordenada> DescartesWumpus  = new HashSet<Coordenada>();

    private bool tieneOro    = false;
    private bool tieneFlecha = true;
    public bool JuegoTerminado { get; private set; } = false;

    // Historial de percepciones por celda (necesario para re-inferir al matar Wumpus)
    private Dictionary<Coordenada, Percepcion> HistorialSensores = new Dictionary<Coordenada, Percepcion>();

    // Ruta planificada por A* o BFS; el agente la sigue paso a paso
    private Queue<Coordenada> rutaPlanificada = new Queue<Coordenada>();

    public AgenteIA(Tablero mundo)
    {
        tableroMundo = mundo;
        // R5: la celda inicial siempre es segura
        BaseConocimiento[PosicionActual] = EstadoInferencia.Seguro;
        HistorialCamino.Add(PosicionActual);
        Visitados.Add(PosicionActual);
    }

    // =========================================================================
    // CICLO PRINCIPAL DEL AGENTE
    // =========================================================================
    public void EjecutarTurno()
    {
        if (JuegoTerminado) return;

        Percepcion sensores      = tableroMundo.Matriz[PosicionActual.X, PosicionActual.Y].Sensores;
        Elemento   contenidoReal = tableroMundo.Matriz[PosicionActual.X, PosicionActual.Y].Contenido;

        // Grito → Wumpus muerto → limpiar hedores del historial
        if ((sensores & Percepcion.Grito) != 0)
        {
            GD.Print("[IA] 🔊 Grito del Wumpus detectado. El monstruo ha muerto.");
            LimpiarHedoresDelHistorial();
            tableroMundo.Matriz[PosicionActual.X, PosicionActual.Y].Sensores &= ~Percepcion.Grito;
            rutaPlanificada.Clear(); // Invalidar ruta: el mapa de peligros cambió
        }

        GD.Print($"\n[IA] Posición: {PosicionActual} | Sensores: {sensores}");

        // - Condiciones de fin de juego -------------------
        if (contenidoReal == Elemento.Oro && !tieneOro)
        {
            GD.Print("[IA] 💰 ¡ORO ENCONTRADO! Calculando ruta de retorno con BFS...");
            tieneOro = true;
            rutaPlanificada.Clear();
            // Planificar retorno a [0,0] usando BFS sobre celdas seguras conocidas
            var rutaBFS = BuscarRutaBFS(PosicionActual, new Coordenada(0, 0));
            if (rutaBFS != null)
            {
                GD.Print($"[BFS] Ruta de retorno calculada: {string.Join(" -> ", rutaBFS)}");
                foreach (var paso in rutaBFS) rutaPlanificada.Enqueue(paso);
            }
            else
            {
                GD.Print("[BFS] No se encontró ruta segura directa. Usando backtracking.");
            }
        }

        if (contenidoReal == Elemento.Hoyo || contenidoReal == Elemento.Wumpus)
        {
            GD.Print($"[IA] 💀 Agente muerto en {PosicionActual} — {contenidoReal}.");
            JuegoTerminado = true;
            return;
        }

        if (tieneOro && PosicionActual.X == 0 && PosicionActual.Y == 0)
        {
            GD.Print("[IA] 🎉 ¡VICTORIA! Regresé con el oro.");
            JuegoTerminado = true;
            return;
        }

        // - Inferencia y movimiento ---------------------─
        ActualizarBaseConocimiento(sensores);
        Coordenada siguiente = DecidirProximaCelda();

        if (!siguiente.Equals(PosicionActual))
        {
            // Si retrocedemos un paso, acortar el historial (no duplicar nodos)
            if (HistorialCamino.Count > 1 && siguiente.Equals(HistorialCamino[HistorialCamino.Count - 2]))
            {
                HistorialCamino.RemoveAt(HistorialCamino.Count - 1);
                PosicionActual = siguiente;
            }
            else
            {
                PosicionActual = siguiente;
                Visitados.Add(PosicionActual);
                HistorialCamino.Add(PosicionActual);
            }
        }
        else
        {
            GD.Print("[IA] 🛑 Sin movimientos posibles. Agente bloqueado.");
            JuegoTerminado = true;
        }
    }

    // =========================================================================
    // BÚSQUEDA NO INFORMADA — BFS (Breadth-First Search)
    // =========================================================================
    // Encuentra la ruta más corta entre 'origen' y 'destino' transitando
    // únicamente por celdas que el agente considera seguras o ya visitadas.
    // Complejidad: O(V + E) donde V = celdas del tablero, E = conexiones.
    //
    // Uso principal: retorno a [0,0] con el oro.
    // =========================================================================
    public List<Coordenada> BuscarRutaBFS(Coordenada origen, Coordenada destino)
    {
        GD.Print($"[BFS] Buscando ruta de {origen} a {destino}...");

        var cola      = new Queue<List<Coordenada>>();
        var visitados = new HashSet<Coordenada>();

        cola.Enqueue(new List<Coordenada> { origen });
        visitados.Add(origen);

        while (cola.Count > 0)
        {
            var camino = cola.Dequeue();
            var actual = camino[camino.Count - 1];

            if (actual.Equals(destino))
            {
                // Excluir el nodo de origen (ya estamos ahí) → devolver solo los pasos siguientes
                var resultado = new List<Coordenada>(camino);
                resultado.RemoveAt(0);
                GD.Print($"[BFS] Ruta encontrada en {resultado.Count} pasos.");
                return resultado;
            }

            foreach (var vecino in ObtenerAdyacentes(actual))
            {
                if (visitados.Contains(vecino)) continue;

                // BFS solo transita por celdas seguras o la meta misma
                bool esSeguroOVisitado = Visitados.Contains(vecino) ||
                    (BaseConocimiento.TryGetValue(vecino, out var estado) &&
                     (estado == EstadoInferencia.Seguro));
                bool esMeta = vecino.Equals(destino);

                if (esSegurosOVisitado(vecino) || esMeta)
                {
                    visitados.Add(vecino);
                    var nuevoCamino = new List<Coordenada>(camino) { vecino };
                    cola.Enqueue(nuevoCamino);
                }
            }
        }

        GD.Print("[BFS] No se encontró ruta segura al destino.");
        return null;
    }

    // =========================================================================
    // BÚSQUEDA INFORMADA — A* Recursivo (A-Star)
    // =========================================================================
    // Encuentra la ruta de menor costo estimado entre 'origen' y 'destino'
    // usando heurística de distancia Manhattan: h(n) = |nx-dx| + |ny-dy|
    //
    // f(n) = g(n) + h(n)
    //   g(n) = costo real acumulado desde el origen
    //   h(n) = estimación optimista al destino (Manhattan)
    //
    // El método público inicializa las estructuras y lanza la recursión.
    // El método privado AEstrella_Recursivo() expande un nodo por llamada:
    //   - Caso base: nodo actual == destino → retorna ruta reconstruida
    //   - Paso recursivo: selecciona el vecino con menor f(n) y se llama a sí mismo
    //
    // Uso principal: navegar hacia la celda segura sin visitar más cercana.
    // =========================================================================
    public List<Coordenada> BuscarRutaAEstrella(Coordenada origen, Coordenada destino)
    {
        GD.Print($"[A*] Buscando ruta óptima de {origen} a {destino}...");

        var costoDesde = new Dictionary<Coordenada, int>();   // g(n) por nodo
        var padre      = new Dictionary<Coordenada, Coordenada>(); // para reconstruir ruta
        var cerrados   = new HashSet<Coordenada>();           // nodos ya expandidos
        // Lista de abiertos ordenada por f(n): cada entrada es (f, coordenada)
        var abiertos   = new List<(int f, Coordenada c)>();

        costoDesde[origen] = 0;
        abiertos.Add((HeuristicaManhattan(origen, destino), origen));

        return AEstrella_Recursivo(destino, abiertos, costoDesde, padre, cerrados);
    }

    // - Núcleo recursivo de A* ------------------------
    // Cada invocación expande el nodo con menor f(n) de la lista de abiertos.
    // Retorna la ruta completa al encontrar el destino, o null si no hay solución.
    private List<Coordenada> AEstrella_Recursivo(
        Coordenada destino,
        List<(int f, Coordenada c)> abiertos,
        Dictionary<Coordenada, int> costoDesde,
        Dictionary<Coordenada, Coordenada> padre,
        HashSet<Coordenada> cerrados)
    {
        // Caso base: sin nodos por explorar → no existe ruta
        if (abiertos.Count == 0)
        {
            GD.Print("[A*] No se encontró ruta al destino.");
            return null;
        }

        // Seleccionar el nodo con menor f(n) (equivale a extraer de cola de prioridad)
        abiertos.Sort((a, b) => a.f.CompareTo(b.f));
        var (_, actual) = abiertos[0];
        abiertos.RemoveAt(0);

        // Caso base: llegamos al destino → reconstruir y retornar ruta
        if (actual.Equals(destino))
        {
            Coordenada raiz = ObtenerOrigen(padre, actual);
            var ruta = ReconstruirRuta(padre, actual, raiz);
            GD.Print($"[A*] Ruta óptima encontrada: {ruta.Count} pasos.");
            return ruta;
        }

        cerrados.Add(actual);

        // Paso recursivo: expandir vecinos y actualizar costos
        foreach (var vecino in ObtenerAdyacentes(actual))
        {
            if (cerrados.Contains(vecino)) continue;
            if (!esSegurosOVisitado(vecino) && !vecino.Equals(destino)) continue;

            int gVecino = costoDesde[actual] + 1;
            if (costoDesde.TryGetValue(vecino, out int gAnterior) && gVecino >= gAnterior) continue;

            padre[vecino]      = actual;
            costoDesde[vecino] = gVecino;
            int f = gVecino + HeuristicaManhattan(vecino, destino);

            // Agregar o actualizar en abiertos
            abiertos.RemoveAll(e => e.c.Equals(vecino));
            abiertos.Add((f, vecino));
        }

        // Llamada recursiva con los abiertos actualizados
        return AEstrella_Recursivo(destino, abiertos, costoDesde, padre, cerrados);
    }

    // - Recorre el diccionario padre hasta encontrar el nodo raíz ------
    private Coordenada ObtenerOrigen(Dictionary<Coordenada, Coordenada> padre, Coordenada nodo)
    {
        while (padre.ContainsKey(nodo)) nodo = padre[nodo];
        return nodo;
    }

    // - Heurística admisible: distancia Manhattan --------------─
    private int HeuristicaManhattan(Coordenada a, Coordenada b)
        => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    private List<Coordenada> ReconstruirRuta(Dictionary<Coordenada, Coordenada> padre,
                                              Coordenada actual, Coordenada origen)
    {
        var ruta = new List<Coordenada>();
        while (!actual.Equals(origen))
        {
            ruta.Add(actual);
            actual = padre[actual];
        }
        ruta.Reverse();
        return ruta;
    }

    // - Predicado compartido: ¿puede el agente transitar por esta celda? ---
    private bool esSegurosOVisitado(Coordenada c)
        => Visitados.Contains(c) ||
           (BaseConocimiento.TryGetValue(c, out var e) && e == EstadoInferencia.Seguro);

    // =========================================================================
    // INFERENCIA LÓGICA PROPOSICIONAL — ActualizarBaseConocimiento
    // =========================================================================
    // Aplica las reglas R1-R7 sobre el historial de sensores para clasificar
    // cada celda del tablero en: Seguro, PosibleHoyo, HoyoConfirmado,
    // PosibleWumpus, WumpusConfirmado o Desconocido.
    // =========================================================================
    private void ActualizarBaseConocimiento(Percepcion sensoresActuales)
    {
        HistorialSensores[PosicionActual] = sensoresActuales;

        SospechasHoyo.Clear();
        SospechasWumpus.Clear();
        DescartesHoyo.Clear();
        DescartesWumpus.Clear();

        // R5: toda celda visitada es segura por definición
        foreach (var v in Visitados)
        {
            DescartesHoyo.Add(v);
            DescartesWumpus.Add(v);
        }

        // R1-R4: recorrer historial de sensores y propagar reglas proposicionales
        foreach (var hecho in HistorialSensores)
        {
            Coordenada c = hecho.Key;
            Percepcion p = hecho.Value;
            var ady = ObtenerAdyacentes(c);

            // R1: ¬Brisa(c) → ¬Hoyo en todos los adyacentes
            if ((p & Percepcion.Brisa) == 0)
                foreach (var v in ady) DescartesHoyo.Add(v);
            else
                // R2: Brisa(c) → posible Hoyo en adyacentes no visitados
                foreach (var v in ady) if (!Visitados.Contains(v)) SospechasHoyo.Add(v);

            // R3: ¬Hedor(c) → ¬Wumpus en todos los adyacentes
            if ((p & Percepcion.Hedor) == 0)
                foreach (var v in ady) DescartesWumpus.Add(v);
            else
                // R4: Hedor(c) → posible Wumpus en adyacentes no visitados
                foreach (var v in ady) if (!Visitados.Contains(v)) SospechasWumpus.Add(v);
        }

        // R6-R7: INFERENCIA POR ELIMINACIÓN
        // Si para una celda con Brisa, todos los vecinos sospechosos menos uno
        // fueron descartados → ese uno es HOYO CONFIRMADO (y lo mismo para Wumpus)
        var hoyoConfirmado   = new HashSet<Coordenada>();
        var wumpusConfirmado = new HashSet<Coordenada>();

        foreach (var hecho in HistorialSensores)
        {
            Coordenada c = hecho.Key;
            Percepcion p = hecho.Value;

            // R6
            if ((p & Percepcion.Brisa) != 0)
            {
                var candidatos = new List<Coordenada>();
                foreach (var v in ObtenerAdyacentes(c))
                    if (!DescartesHoyo.Contains(v)) candidatos.Add(v);
                if (candidatos.Count == 1) hoyoConfirmado.Add(candidatos[0]);
            }

            // R7
            if ((p & Percepcion.Hedor) != 0)
            {
                var candidatos = new List<Coordenada>();
                foreach (var v in ObtenerAdyacentes(c))
                    if (!DescartesWumpus.Contains(v)) candidatos.Add(v);
                if (candidatos.Count == 1) wumpusConfirmado.Add(candidatos[0]);
            }
        }

        // Actualizar BaseConocimiento con los estados inferidos
        int d = tableroMundo.Dimension;
        for (int x = 0; x < d; x++)
        {
            for (int y = 0; y < d; y++)
            {
                var v = new Coordenada(x, y);
                if (Visitados.Contains(v)) continue;

                bool esHoyoDesc   = DescartesHoyo.Contains(v);
                bool esWumpusDesc = DescartesWumpus.Contains(v) || !tableroMundo.WumpusVivo;
                bool esHoyoConf   = hoyoConfirmado.Contains(v);
                bool esWumpusConf = wumpusConfirmado.Contains(v);

                EstadoInferencia estadoAnterior = BaseConocimiento.TryGetValue(v, out var ea) ? ea : EstadoInferencia.Desconocido;
                EstadoInferencia estadoNuevo;

                if (esHoyoConf)
                    estadoNuevo = EstadoInferencia.HoyoConfirmado;
                else if (esWumpusConf && !esWumpusDesc)
                    estadoNuevo = EstadoInferencia.WumpusConfirmado;
                else if (esHoyoDesc && esWumpusDesc)
                    estadoNuevo = EstadoInferencia.Seguro;
                else if (!esWumpusDesc && SospechasWumpus.Contains(v))
                    estadoNuevo = EstadoInferencia.PosibleWumpus;
                else if (!esHoyoDesc && SospechasHoyo.Contains(v))
                    estadoNuevo = EstadoInferencia.PosibleHoyo;
                else
                    estadoNuevo = EstadoInferencia.Desconocido;

                BaseConocimiento[v] = estadoNuevo;

                if (estadoNuevo != estadoAnterior)
                    GD.Print($"[IA] 🧠 Inferencia: {v} → {estadoNuevo}");
            }
        }
    }

    // =========================================================================
    // DECISOR DE MOVIMIENTO — usa BFS / A* según el estado del agente
    // =========================================================================
    private Coordenada DecidirProximaCelda()
    {
        // - CON ORO: seguir la ruta BFS precalculada hacia [0,0] ------─
        if (tieneOro)
        {
            if (rutaPlanificada.Count > 0)
                return rutaPlanificada.Dequeue();

            // Fallback: BFS reactivo si la ruta se agotó (no debería pasar)
            var ruta = BuscarRutaBFS(PosicionActual, new Coordenada(0, 0));
            if (ruta != null && ruta.Count > 0)
            {
                foreach (var paso in ruta) rutaPlanificada.Enqueue(paso);
                return rutaPlanificada.Dequeue();
            }
            // Último recurso: retroceder por historial
            if (HistorialCamino.Count > 1)
                return HistorialCamino[HistorialCamino.Count - 2];
            return PosicionActual;
        }

        // - SIN ORO: usar ruta planificada por A* si existe ---------
        if (rutaPlanificada.Count > 0)
        {
            var siguiente = rutaPlanificada.Peek();
            // Verificar que el siguiente paso sigue siendo seguro (inferencia pudo cambiar)
            if (esSegurosOVisitado(siguiente))
                return rutaPlanificada.Dequeue();
            else
                rutaPlanificada.Clear(); // Ruta invalidada por nueva inferencia
        }

        var adyacentes = ObtenerAdyacentes(PosicionActual);

        // PRIORIDAD 1: Celda adyacente segura no visitada (movimiento directo)
        foreach (var opc in adyacentes)
        {
            if (!Visitados.Contains(opc) &&
                BaseConocimiento.TryGetValue(opc, out var e) &&
                e == EstadoInferencia.Seguro)
                return opc;
        }

        // PRIORIDAD 2: Hay celdas seguras no visitadas en otro lugar del mapa
        // → usar A* para llegar a la más cercana por camino seguro
        Coordenada? metaAEstrella = EncontrarCeldaSeguraMasCercana();
        if (metaAEstrella.HasValue)
        {
            var ruta = BuscarRutaAEstrella(PosicionActual, metaAEstrella.Value);
            if (ruta != null && ruta.Count > 0)
            {
                GD.Print($"[A*] Navegando hacia celda segura {metaAEstrella.Value}");
                foreach (var paso in ruta) rutaPlanificada.Enqueue(paso);
                return rutaPlanificada.Dequeue();
            }
        }

        // PRIORIDAD 3: Usar flecha si hay Wumpus identificado
        if (tieneFlecha && tableroMundo.WumpusVivo)
        {
            var resultado = IntentarDispararFlecha();
            if (resultado.HasValue) return resultado.Value;
        }

        // PRIORIDAD 4: Riesgo calculado — evitar solo peligros confirmados
        foreach (var opc in adyacentes)
        {
            if (!Visitados.Contains(opc) &&
                BaseConocimiento.TryGetValue(opc, out var e) &&
                e != EstadoInferencia.HoyoConfirmado &&
                e != EstadoInferencia.WumpusConfirmado)
            {
                GD.Print($"[IA] 🎲 Riesgo calculado hacia: {opc}");
                return opc;
            }
        }

        // PRIORIDAD 5: Backtracking por historial
        if (HistorialCamino.Count > 1)
        {
            GD.Print("[IA] ↩️ Backtracking por historial...");
            return HistorialCamino[HistorialCamino.Count - 2];
        }

        return PosicionActual;
    }

    // - Encuentra la celda segura no visitada con menor distancia Manhattan -
    private Coordenada? EncontrarCeldaSeguraMasCercana()
    {
        Coordenada? mejor  = null;
        int         minDist = int.MaxValue;

        foreach (var nodo in BaseConocimiento)
        {
            if (nodo.Value == EstadoInferencia.Seguro && !Visitados.Contains(nodo.Key))
            {
                int dist = HeuristicaManhattan(PosicionActual, nodo.Key);
                if (dist < minDist) { minDist = dist; mejor = nodo.Key; }
            }
        }
        return mejor;
    }

    // - Lógica de disparo de flecha ---------------------─
    private Coordenada? IntentarDispararFlecha()
    {
        Coordenada objetivoWumpus = default;
        bool encontro = false;

        foreach (var nodo in BaseConocimiento)
        {
            if ((nodo.Value == EstadoInferencia.WumpusConfirmado ||
                 nodo.Value == EstadoInferencia.PosibleWumpus) &&
                !Visitados.Contains(nodo.Key))
            {
                objetivoWumpus = nodo.Key;
                encontro = true;
                if (nodo.Value == EstadoInferencia.WumpusConfirmado) break;
            }
        }

        if (!encontro) return null;

        var ady = ObtenerAdyacentes(objetivoWumpus);
        if (!ady.Contains(PosicionActual))
        {
            if (HistorialCamino.Count > 1)
            {
                GD.Print("[IA] ↩️ Aproximándome al Wumpus para disparar...");
                return HistorialCamino[HistorialCamino.Count - 2];
            }
            return null;
        }

        var dir = new Coordenada(
            objetivoWumpus.X - PosicionActual.X,
            objetivoWumpus.Y - PosicionActual.Y);

        GD.Print($"[IA] 🏹 Disparando flecha hacia {objetivoWumpus}...");
        tieneFlecha = false;
        bool golpeo = tableroMundo.RegistrarDisparoFlecha(PosicionActual, dir);

        if (golpeo)
        {
            GD.Print("[IA] 🎯 ¡Wumpus eliminado! Recalculando mapa...");
            LimpiarHedoresDelHistorial();
        }
        else
        {
            GD.Print($"[IA] 🧠 Fallo. {objetivoWumpus} descartada como peligro Wumpus.");
        }

        BaseConocimiento[objetivoWumpus] = EstadoInferencia.Seguro;
        rutaPlanificada.Clear();
        ActualizarBaseConocimiento(tableroMundo.Matriz[PosicionActual.X, PosicionActual.Y].Sensores);
        return DecidirProximaCelda();
    }

    // - Limpia hedores del historial tras morir el Wumpus ----------
    private void LimpiarHedoresDelHistorial()
    {
        var llaves = new List<Coordenada>(HistorialSensores.Keys);
        foreach (var coord in llaves)
            HistorialSensores[coord] &= ~Percepcion.Hedor;
    }

    // - Obtiene las celdas adyacentes válidas (dentro del tablero) ------
    private List<Coordenada> ObtenerAdyacentes(Coordenada c)
    {
        var lista = new List<Coordenada>();
        int d = tableroMundo.Dimension;
        if (c.X + 1 < d)  lista.Add(new Coordenada(c.X + 1, c.Y));
        if (c.X - 1 >= 0) lista.Add(new Coordenada(c.X - 1, c.Y));
        if (c.Y + 1 < d)  lista.Add(new Coordenada(c.X, c.Y + 1));
        if (c.Y - 1 >= 0) lista.Add(new Coordenada(c.X, c.Y - 1));
        return lista;
    }

    // - Accesores públicos --------------------------
    public bool ObtenerEstadoTieneOro()    => tieneOro;
    public bool ObtenerEstadoTieneFlecha() => tieneFlecha;
}