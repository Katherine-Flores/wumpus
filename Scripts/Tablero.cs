using System;
using System.Collections.Generic;
using Godot;

public class Tablero
{
    public int Dimension { get; private set; }
    public Celda[,] Matriz { get; private set; }
    public Coordenada PosicionOro { get; private set; }
    public bool WumpusVivo { get; private set; } = true;

    public Tablero(int dimension, int numHoyos, int numWumpus)
    {
        Dimension = dimension;
        bool mapaValido = false;
        int intentos = 0;

        while (!mapaValido && intentos < 500)
        {
            intentos++;
            InicializarMatriz();
            ColocarElementosAleatorios(numHoyos, numWumpus);
            GenerarPercepciones();
            
            // Validación estricta: BFS desde [0,0] buscando el Oro por camino 100% SEGURO
            mapaValido = ValidarCaminoSeguro();
        }

        GD.Print($"[TABLERO] Mapa generado con éxito en el intento: {intentos}");
    }

    private void InicializarMatriz()
    {
        Matriz = new Celda[Dimension, Dimension];
        for (int x = 0; x < Dimension; x++)
            for (int y = 0; y < Dimension; y++)
                Matriz[x, y] = new Celda();
    }

    private void ColocarElementosAleatorios(int hoyos, int wumpus)
    {
        Random rand = new Random();
        
        // Colocar Oro obligatoriamente
        ColocarElementoAleatorio(Elemento.Oro, rand);

        // Colocar Hoyos
        for (int i = 0; i < hoyos; i++) 
            ColocarElementoAleatorio(Elemento.Hoyo, rand);

        // Colocar Wumpus
        for (int i = 0; i < wumpus; i++) 
            ColocarElementoAleatorio(Elemento.Wumpus, rand);
    }

    private void ColocarElementoAleatorio(Elemento elemento, Random rand)
    {
        while (true)
        {
            int x = rand.Next(Dimension);
            int y = rand.Next(Dimension);

            // No sobreescribir celdas ocupadas, ni la salida [0,0]
            if ((x == 0 && y == 0) || Matriz[x, y].Contenido != Elemento.Vacio)
                continue;

            Matriz[x, y].Contenido = elemento;
            if (elemento == Elemento.Oro) PosicionOro = new Coordenada(x, y);
            break;
        }
    }

    private void GenerarPercepciones()
    {
        for (int x = 0; x < Dimension; x++)
        {
            for (int y = 0; y < Dimension; y++)
            {
                if (Matriz[x, y].Contenido == Elemento.Hoyo)
                    ModificarAdyacentes(x, y, Percepcion.Brisa);
                
                if (Matriz[x, y].Contenido == Elemento.Wumpus)
                    ModificarAdyacentes(x, y, Percepcion.Hedor);
                
                if (Matriz[x, y].Contenido == Elemento.Oro)
                    Matriz[x, y].Sensores |= Percepcion.Resplandor;
            }
        }
    }

    private void ModificarAdyacentes(int x, int y, Percepcion flag)
    {
        Coordenada[] adyacentes = {
            new Coordenada(x+1, y), new Coordenada(x-1, y),
            new Coordenada(x, y+1), new Coordenada(x, y-1)
        };

        foreach (var c in adyacentes)
        {
            if (c.X >= 0 && c.X < Dimension && c.Y >= 0 && c.Y < Dimension)
            {
                Matriz[c.X, c.Y].Sensores |= flag;
            }
        }
    }

    private bool ValidarCaminoSeguro()
    {
        Queue<Coordenada> cola = new Queue<Coordenada>();
        HashSet<Coordenada> visitados = new HashSet<Coordenada>();
        
        Coordenada inicio = new Coordenada(0, 0);
        cola.Enqueue(inicio);
        visitados.Add(inicio);

        while (cola.Count > 0)
        {
            Coordenada actual = cola.Dequeue();

            if (actual.Equals(PosicionOro)) return true;

            Coordenada[] adyacentes = {
                new Coordenada(actual.X+1, actual.Y), new Coordenada(actual.X-1, actual.Y),
                new Coordenada(actual.X, actual.Y+1), new Coordenada(actual.X, actual.Y-1)
            };

            foreach (var vecino in adyacentes)
            {
                if (vecino.X >= 0 && vecino.X < Dimension && vecino.Y >= 0 && vecino.Y < Dimension)
                {
                    // Un camino es seguro si está vacío o si es la meta de oro misma
                    if (!visitados.Contains(vecino) && 
                        (Matriz[vecino.X, vecino.Y].Contenido == Elemento.Vacio || Matriz[vecino.X, vecino.Y].Contenido == Elemento.Oro))
                    {
                        visitados.Add(vecino);
                        cola.Enqueue(vecino);
                    }
                }
            }
        }
        return false;
    }

    // LOG AUXILIAR: Devuelve un mapa visual en strings para la consola
    public void ImprimirMapaConsola()
    {
        GD.Print("\n--- [VISTA DEL MAPA REAL] ---");
        for (int y = Dimension - 1; y >= 0; y--)
        {
            string fila = "";
            for (int x = 0; x < Dimension; x++)
            {
                char rep = '.';
                if (x == 0 && y == 0) rep = 'A'; // Agente inicio
                else if (Matriz[x, y].Contenido == Elemento.Hoyo) rep = 'H';
                else if (Matriz[x, y].Contenido == Elemento.Wumpus) rep = 'W';
                else if (Matriz[x, y].Contenido == Elemento.Oro) rep = 'O';
                
                fila += $"[{rep}] ";
            }
            GD.Print(fila);
        }
    }

    public bool RegistrarDisparoFlecha(Coordenada desde, Coordenada direccion)
    {
        if (!WumpusVivo) return false;

        // Proyectar el disparo en la dirección elegida en línea recta
        int revisarX = desde.X + direccion.X;
        int revisarY = desde.Y + direccion.Y;

        // El disparo viaja por toda la fila/columna hasta el borde del mapa
        while (revisarX >= 0 && revisarX < Dimension && revisarY >= 0 && revisarY < Dimension)
        {
            if (Matriz[revisarX, revisarY].Contenido == Elemento.Wumpus)
            {
                // ¡Impacto! Eliminar al Wumpus del mapa real
                Matriz[revisarX, revisarY].Contenido = Elemento.Vacio;
                WumpusVivo = false;
                
                // Eliminar los hedores de todo el tablero
                LimpiarHedoresDelMapa();

                // Emitir el Grito en la celda actual del agente para que lo perciba este turno
                Matriz[desde.X, desde.Y].Sensores |= Percepcion.Grito;
                
                GD.Print("[TABLERO] 🎯 ¡El Wumpus ha sido aniquilado! Se escucha un eco en la cueva.");
                return true; 
            }
            revisarX += direccion.X;
            revisarY += direccion.Y;
        }

        GD.Print("[TABLERO] 🏹 La flecha se estrelló contra la pared. El Wumpus sigue vivo.");
        return false;
    }

    private void LimpiarHedoresDelMapa()
    {
        for (int x = 0; x < Dimension; x++)
        {
            for (int y = 0; y < Dimension; y++)
            {
                // Quitamos la bandera de Hedor usando operaciones de bits
                Matriz[x, y].Sensores &= ~Percepcion.Hedor;
            }
        }
    }
}