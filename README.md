# Mundo de Wumpus — Agente Inteligente

Proyecto final del curso de Inteligencia Artificial. Implementa un agente autónomo capaz de explorar el Mundo de Wumpus, inferir información del entorno con conocimiento parcial, localizar el oro y regresar de forma segura al punto de inicio.

Desarrollado en **C# con Godot Engine 4**.

---

## Estructura del proyecto

```
wumpus/
├── Types.cs           — Tipos y enumeraciones compartidas
├── Celda.cs           — Modelo de una casilla del tablero
├── Tablero.cs         — Generación y gestión del mundo
├── AgenteIA.cs        — Agente inteligente (inferencia + búsquedas)
└── MainSimulation.cs  — Bucle de simulación y HUD de Godot
```

---

## Descripción de cada archivo

### `Types.cs` — Tipos compartidos

Define los tipos de datos utilizados en todo el proyecto:

| Tipo               | Descripción                                                                                                                             |
| ------------------ | --------------------------------------------------------------------------------------------------------------------------------------- |
| `Elemento`         | Enum: `Vacio`, `Hoyo`, `Wumpus`, `Oro` — contenido de una celda                                                                         |
| `Percepcion`       | Flags enum: `Brisa`, `Hedor`, `Resplandor`, `Grito` — sensores del agente                                                               |
| `EstadoInferencia` | Enum: `Desconocido`, `Seguro`, `PosibleHoyo`, `HoyoConfirmado`, `PosibleWumpus`, `WumpusConfirmado` — estado en la base de conocimiento |
| `Coordenada`       | Struct con `X`, `Y`, implementa `Equals` y `GetHashCode` para usarse en `HashSet` y `Dictionary`                                        |

`Percepcion` usa el atributo `[Flags]` para permitir combinaciones con operaciones de bits (`|`, `&`, `~`), lo que permite representar varios sensores activos simultáneamente en un solo campo.

---

### `Celda.cs` — Unidad básica del tablero

Representa una habitación del laberinto.

```
Celda
├── Contenido : Elemento   → qué hay en esa casilla (hoyo, wumpus, oro, vacío)
└── Sensores  : Percepcion → qué percepciones emite esa casilla (brisa, hedor, etc.)
```

Ambas propiedades tienen valores por defecto (`Vacio` y `Ninguna`) para que el tablero parta limpio.

---

### `Tablero.cs` — Generación y gestión del mundo

Construye el entorno del juego y expone la matriz de celdas al agente.

#### Construcción del tablero

El constructor recibe `dimension`, `numHoyos` y `numWumpus`. Genera el mapa en un bucle con hasta 500 intentos, descartando mapas donde no exista un camino seguro al oro:

```
while (!mapaValido && intentos < 500):
    InicializarMatriz()        → crea la cuadrícula vacía
    ColocarElementosAleatorios → ubica oro, hoyos y wumpus aleatoriamente
    GenerarPercepciones()      → propaga brisas y hedores a celdas adyacentes
    mapaValido = ValidarCaminoSeguro()  ← BFS interno de validación
```

#### `GenerarPercepciones()`

Recorre la matriz y para cada elemento peligroso llama a `ModificarAdyacentes`, que activa el flag de sensor correspondiente en las cuatro celdas vecinas (Norte, Sur, Este, Oeste). El oro activa `Resplandor` solo en su propia celda.

#### `ValidarCaminoSeguro()` — BFS de validación interna

**Este BFS es un mecanismo de generación, no de navegación del agente.** Verifica que exista al menos un camino desde `[0,0]` hasta el oro transitando únicamente por celdas vacías. Si no existe ese camino, el mapa se descarta y se regenera. Garantiza que el juego siempre tenga solución.

#### `RegistrarDisparoFlecha(desde, direccion)`

Proyecta la flecha en línea recta desde la posición del agente en la dirección indicada. Si alcanza al Wumpus:

- Elimina al Wumpus del mapa real (`Elemento.Vacio`)
- Llama a `LimpiarHedoresDelMapa()` para quitar todos los bits de `Hedor`
- Emite `Percepcion.Grito` en la celda del agente para que lo perciba en el mismo turno

#### `ImprimirMapaConsola()`

Imprime el mapa real completo al inicio de la simulación (vista de dios) con los símbolos: `A` = inicio del agente, `H` = hoyo, `W` = wumpus, `O` = oro, `.` = vacío.

---

### `AgenteIA.cs` — Núcleo de inteligencia artificial

El archivo más importante del proyecto. Implementa cuatro componentes de IA que trabajan juntos:

#### 1. Lógica Proposicional — Base de Conocimiento

El agente mantiene en `BaseConocimiento` (un `Dictionary<Coordenada, EstadoInferencia>`) el estado inferido de cada celda. Esta base se actualiza en cada turno aplicando las siguientes **reglas proposicionales**:

| Regla | Proposición lógica                              | Acción                                   |
| ----- | ----------------------------------------------- | ---------------------------------------- |
| R1    | `¬Brisa(x,y) → ∀ adyacente(a): ¬Hoyo(a)`        | Descartar hoyo en vecinos                |
| R2    | `Brisa(x,y) → ∃ adyacente(a): Hoyo(a)`          | Sospechar hoyo en vecinos no visitados   |
| R3    | `¬Hedor(x,y) → ∀ adyacente(a): ¬Wumpus(a)`      | Descartar wumpus en vecinos              |
| R4    | `Hedor(x,y) → ∃ adyacente(a): Wumpus(a)`        | Sospechar wumpus en vecinos no visitados |
| R5    | `Visitado(x,y) → Seguro(x,y)`                   | Toda celda visitada es segura            |
| R6    | `Candidatos_Brisa(c) = 1 → HoyoConfirmado(c)`   | Inferencia por eliminación               |
| R7    | `Candidatos_Hedor(c) = 1 → WumpusConfirmado(c)` | Inferencia por eliminación               |

Las reglas R6 y R7 implementan **inferencia por eliminación**: si una celda con brisa tenía N vecinos candidatos a hoyo, y N-1 de ellos fueron descartados por otras reglas, el candidato restante es un hoyo confirmado.

Las reglas se aplican sobre el `HistorialSensores`, un diccionario que recuerda las percepciones de cada celda visitada. Esto permite que al matar al Wumpus (y limpiar los hedores del historial), la inferencia se recalcule correctamente en el siguiente turno.

#### 2. Búsqueda No Informada — BFS

**Método:** `BuscarRutaBFS(origen, destino) → List<Coordenada>`

Implementa Búsqueda en Anchura (Breadth-First Search) sobre las celdas que el agente considera transitables (visitadas o inferidas como seguras).

```
Algoritmo BFS:
  cola ← [[origen]]
  visitados ← {origen}
  mientras cola no vacía:
      camino ← cola.Desencolar()
      actual ← camino.último
      si actual == destino → retornar camino sin el origen
      para cada vecino de actual:
          si vecino es seguro o visitado y no fue visitado:
              cola.Encolar(camino + [vecino])
```

**Garantía:** BFS siempre encuentra el camino con **menor número de pasos** (óptimo en grafos sin pesos). Complejidad O(V + E).

**Uso en el proyecto:** Se invoca cuando el agente recoge el oro para calcular la ruta más corta de regreso a `[0,0]`. La ruta completa se guarda en `rutaPlanificada` y el agente la sigue paso a paso en turnos sucesivos.

#### 3. Búsqueda Informada — A\*

**Método:** `BuscarRutaAEstrella(origen, destino) → List<Coordenada>`

Implementa el algoritmo A\* con heurística de distancia Manhattan.

```
f(n) = g(n) + h(n)
  g(n) = costo real acumulado desde el origen (número de pasos)
  h(n) = distancia Manhattan al destino = |nx - dx| + |ny - dy|
```

La heurística Manhattan es **admisible** (nunca sobreestima) porque en una cuadrícula sin movimientos diagonales es el mínimo número de pasos posibles.

```
Algoritmo A*:
  abiertos ← cola de prioridad ordenada por f(n)
  abiertos.agregar(origen, f=h(origen))
  costoDesde[origen] = 0
  mientras abiertos no vacío:
      actual ← extraer nodo con menor f
      si actual == destino → reconstruir ruta desde padre[]
      para cada vecino de actual:
          si vecino es transitable y g_nuevo < g_anterior:
              padre[vecino] = actual
              costoDesde[vecino] = g_nuevo
              abiertos.agregar(vecino, f = g_nuevo + h(vecino))
```

**Uso en el proyecto:** Se invoca durante la exploración cuando no hay celdas seguras adyacentes directas. El método `EncontrarCeldaSeguraMasCercana()` identifica la celda segura sin visitar con menor distancia Manhattan, y A\* calcula la ruta óptima hacia ella a través del mapa conocido.

#### 4. Decisor de movimiento — `DecidirProximaCelda()`

Orquesta los tres sistemas anteriores según el estado del agente:

```
Si tieneOro:
    → seguir ruta BFS precalculada a [0,0]

Si hay ruta A* planificada y sigue siendo válida:
    → seguir el siguiente paso de esa ruta

Prioridad 1: celda adyacente segura no visitada (movimiento directo)
Prioridad 2: celda segura lejana → llamar A* para planificar ruta
Prioridad 3: disparar flecha si hay Wumpus identificado
Prioridad 4: celda de riesgo (evitando solo peligros confirmados)
Prioridad 5: backtracking por historial de camino
```

La invalidación de ruta planificada ocurre cuando:

- El agente mata al Wumpus (el mapa de peligros cambia)
- El siguiente paso de la ruta ya no es seguro según la inferencia actualizada

---

### `MainSimulation.cs` — Bucle de simulación y HUD

Nodo raíz de Godot que conecta la lógica de IA con la interfaz gráfica.

#### `_Ready()`

Se ejecuta una sola vez al inicio:

1. Configura los parámetros del tablero (`dimensionTablero`, `totalHoyos`, `totalWumpus`)
2. Instancia `Tablero` (genera y valida el mapa)
3. Instancia `AgenteIA` con referencia al tablero
4. Vincula los nodos de Godot: `AnimationPlayer`, `Label` de sensores, `Label` de historial, `Sprite2D` de cueva normal/oro, `AnimatedSprite2D` del Wumpus
5. Muestra las percepciones iniciales de `[0,0]` en el HUD

#### `_Process(delta)`

Se ejecuta cada frame. Usa un temporizador (`temporizadorTurno`) para ejecutar un turno del agente cada `tiempoPorPaso` segundos (por defecto 2.0 s), dando tiempo a la animación de transición de escena:

```
Cada frame:
    temporizadorTurno += delta
    si temporizador >= (tiempo/2) y no disparó transición:
        → reproducir animación "cambio_escena"
    si temporizador >= tiempo:
        → ejecutar inteligencia.EjecutarTurno()
        → actualizar HUD con nuevos sensores
        → actualizar historial de ruta en pantalla
        → cambiar sprite de fondo (cueva normal vs cueva con oro)
        → si juego terminó → llamar ManejarFinDeJuego()
```

#### `ActualizarHUDPrimeraPersona(sensores)`

Traduce las percepciones actuales del agente a descripciones narrativas en primera persona mostradas en el `LabelSensores`. Cada bit de `Percepcion` tiene su texto descriptivo correspondiente.

#### `ManejarFinDeJuego()`

Actualiza el texto del HUD según el desenlace: muerte por hoyo o wumpus, o victoria con el oro en `[0,0]`.

#### `InicializarMiniMapa()` y `ActualizarMiniMapa()`

El minimapa es un `GridContainer` de Godot donde cada celda se construye como un árbol de nodos superpuestos:

```
Control "Celda_X_Y"
  ├── TextureRect "Sprite"      ← sprite del contenido (capa base)
  ├── ColorRect   "Overlay"     ← tinte semitransparente sobre el sprite
  ├── ColorRect   "BordeTop"    ┐
  ├── ColorRect   "BordeBottom" │ cuatro segmentos de 2px
  ├── ColorRect   "BordeLeft"   │ forman el borde visible
  └── ColorRect   "BordeRight"  ┘
```

`InicializarMiniMapa()` crea esta estructura una sola vez en `_Ready()`. `ActualizarMiniMapa()` se llama cada turno y modifica el color del `Overlay` y de los cuatro `Borde*` según el estado de inferencia de la IA para esa celda.

**Importante:** el minimapa muestra el **conocimiento del agente**, no el mapa real. Una celda con hoyo confirmado aparece con su sprite y borde rojo solo si la IA lo dedujo — las celdas que nunca percibió ni infirió permanecen oscuras.

---

## Guía visual del minimapa

Cada celda del minimapa comunica dos cosas simultáneamente: el **sprite** (qué hay en la celda según la IA) y el **color** (qué tan segura o peligrosa la considera).

### Sprites

| Sprite               | Significado                                    |
| -------------------- | ---------------------------------------------- |
| Celda oscura / vacía | Contenido desconocido o celda vacía confirmada |
| Icono de hoyo        | Hoyo confirmado por inferencia o visitado      |
| Icono de Wumpus      | Wumpus confirmado por inferencia o encontrado  |
| Icono de oro         | Oro encontrado al pisar la celda               |

### Colores de overlay y borde

| Color                            | Estado                   | Explicación                                         |
| -------------------------------- | ------------------------ | --------------------------------------------------- |
| **Amarillo** — borde grueso      | Posición actual de la IA | La celda donde se encuentra el agente en este turno |
| **Blanco tenue** — borde fino    | Celda visitada           | El agente pisó esta celda al menos una vez          |
| **Verde** — overlay + borde      | `Seguro` inferido        | La IA dedujo que no hay hoyo ni Wumpus (por R1+R3)  |
| **Naranja** — overlay + borde    | `PosibleHoyo`            | Hay brisa en al menos un vecino visitado            |
| **Rojo suave** — overlay + borde | `PosibleWumpus`          | Hay hedor en al menos un vecino visitado            |
| **Rojo fuerte** — borde grueso   | `HoyoConfirmado`         | Único candidato restante tras eliminación (R6)      |
| **Morado** — borde grueso        | `WumpusConfirmado`       | Único candidato restante tras eliminación (R7)      |
| **Sin color**                    | `Desconocido`            | La IA no tiene información sobre esta celda aún     |

### Relación con la lógica proposicional

Los colores son una representación visual directa de la base de conocimiento del agente. Por ejemplo, si ves una celda naranja adyacente a otra verde, significa que la IA detectó brisa en una celda visitada cercana pero ya descartó otras opciones — esa celda naranja es la sospechosa. Si el naranja se convierte en rojo fuerte, la IA aplicó la regla R6 (eliminación) y ya está segura de que hay un hoyo ahí.

---

## Flujo de ejecución

```
Inicio
  └─ Tablero: generar mapa válido (BFS validación interna)
       └─ AgenteIA: inicializar en [0,0]
            └─ Loop por turno:
                 ├─ Leer sensores de la celda actual
                 ├─ ActualizarBaseConocimiento (R1-R7)
                 ├─ DecidirProximaCelda:
                 │    ├─ [tiene oro]  → seguir ruta BFS a [0,0]
                 │    ├─ [explorar]   → A* a celda segura más cercana
                 │    ├─ [wumpus]     → disparo de flecha
                 │    └─ [riesgo]     → celda no confirmada peligrosa
                 └─ Mover agente → actualizar HUD
```

---

## Cómo modificar el tablero

En `MainSimulation.cs`, método `_Ready()`:

```csharp
int dimensionTablero = 4;  // Cambiar a 3, 5, 6, etc.
int totalHoyos       = 2;  // Número de hoyos
int totalWumpus      = 1;  // Número de Wumpus (el agente tiene una flecha)
```

La velocidad de simulación se controla con:

```csharp
private float tiempoPorPaso = 4.0f;  // Segundos entre turnos del agente
```

---

## Tecnologías

- **Lenguaje:** C# (.NET)
- **Motor:** Godot Engine 4
- **Paradigmas de IA:** Agente basado en conocimiento, lógica proposicional, búsqueda en espacio de estados
- **Integrantes:** Yara Guzman, Katherine Flores, Keny Lopez
