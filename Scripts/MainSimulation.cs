using Godot;
using System;

public partial class MainSimulation : Node
{
    // - Nodos de la Interfaz -------------------------
    private AnimationPlayer  animadorTransicion;
    private Label            labelSensores;
    private RichTextLabel    labelHistorial;
    private Sprite2D         spriteCuevaOro;
    private Sprite2D         spriteCuevaNormal;
    private AnimatedSprite2D wumpus;
    private GridContainer    miniMapa;
    private VideoStreamPlayer videoMuerte;

    // - Lógica del juego ---------------------------
    private Tablero  mundo;
    private AgenteIA inteligencia;
    private float    temporizadorTurno        = 0.0f;
    private float    tiempoPorPaso            = 4.0f;
    private bool     transicionDisparada      = false;
    private bool     teniaFlechaEnTurnoAnterior = true;

    // - Spritesheet del minimapa (se carga una sola vez) -----------
    private Texture2D sheetMinimapa;

    private const int FRAME_VACIO  = 0;
    private const int FRAME_HOYO   = 1;
    private const int FRAME_WUMPUS = 2;
    private const int FRAME_ORO    = 3;
    private const int FRAME_SIZE   = 32;

    // =========================================================================
    public override void _Ready()
    {
        GD.Print("[SISTEMA] Iniciando simulación...");

        int dimensionTablero = 4;
        int totalHoyos       = 2;
        int totalWumpus      = 1;

        mundo        = new Tablero(dimensionTablero, totalHoyos, totalWumpus);
        inteligencia = new AgenteIA(mundo);
        mundo.ImprimirMapaConsola();

        animadorTransicion = GetNode<AnimationPlayer>("AnimationPlayer");

        labelSensores      = GetNode<Label>("CanvasLayer/TextureRect/LabelSensores");
        labelHistorial     = GetNode<RichTextLabel>("CanvasLayer/TextureRect2/LabelHistorial");
        spriteCuevaNormal  = GetNode<Sprite2D>("CanvasLayer/Normal");
        spriteCuevaOro     = GetNode<Sprite2D>("CanvasLayer/Oro");
        wumpus             = GetNode<AnimatedSprite2D>("CanvasLayer/Wumpus");
        videoMuerte        = GetNode<VideoStreamPlayer>("CanvasLayer/VideoMuerte");
        miniMapa           = GetNode<GridContainer>("CanvasLayer/MiniMapa");

        sheetMinimapa = GD.Load<Texture2D>("res://Assets/Sprites/celdas.png");

        InicializarMiniMapa();

        Percepcion sensoresIniciales = mundo.Matriz[0, 0].Sensores;
        ActualizarHUDPrimeraPersona(sensoresIniciales);
        labelHistorial.BbcodeEnabled = true;
        labelHistorial.Text = "[color=white]Ruta mental:[/color]\n[0,0]";
        spriteCuevaNormal.Visible    = true;
        spriteCuevaOro.Visible       = false;
        teniaFlechaEnTurnoAnterior   = inteligencia.ObtenerEstadoTieneFlecha();

        ActualizarMiniMapa();
    }

    // =========================================================================
    public override void _Process(double delta)
    {
        if (inteligencia == null || inteligencia.JuegoTerminado)
        {
            SetProcess(false);
            GD.Print("\n[SISTEMA] Simulación terminada.");
            return;
        }

        temporizadorTurno += (float)delta;

        if (temporizadorTurno >= (tiempoPorPaso / 2.0f) && !transicionDisparada)
        {
            animadorTransicion.Play("cambio_escena");
            transicionDisparada = true;
        }

        if (temporizadorTurno >= tiempoPorPaso)
        {
            temporizadorTurno   = 0.0f;
            transicionDisparada = false;

            inteligencia.EjecutarTurno();

            Coordenada posicionActual         = inteligencia.PosicionActual;
            Elemento   contenidoCasillaActual = mundo.Matriz[posicionActual.X, posicionActual.Y].Contenido;
            Percepcion sensoresActuales       = mundo.Matriz[posicionActual.X, posicionActual.Y].Sensores;

            ActualizarHUDPrimeraPersona(sensoresActuales);
            ActualizarLabelHistorial();
            ActualizarMiniMapa();

            spriteCuevaNormal.Visible = contenidoCasillaActual != Elemento.Oro;
            spriteCuevaOro.Visible    = contenidoCasillaActual == Elemento.Oro;

            if (inteligencia.JuegoTerminado)
                ManejarFinDeJuego();

            GD.Print($"[HISTORIAL] {string.Join(" -> ", inteligencia.HistorialCamino)}");
        }
    }

    // =========================================================================
    // MINIMAPA — Inicialización
    // Estructura por celda (de abajo hacia arriba en el árbol de nodos):
    //
    //   Control "Celda_X_Y"
    //     ├- TextureRect "Sprite"      ← sprite del contenido (capa base)
    //     ├- ColorRect   "Overlay"     ← tinte de color semitransparente
    //     ├- ColorRect   "BordeTop"    ┐
    //     ├- ColorRect   "BordeBottom" │ borde visible de 2px
    //     ├- ColorRect   "BordeLeft"   │ encima de todo
    //     └- ColorRect   "BordeRight"  ┘
    // =========================================================================
    private void InicializarMiniMapa()
    {
        foreach (Node hijo in miniMapa.GetChildren())
            hijo.QueueFree();

        miniMapa.Columns = mundo.Dimension;

        for (int y = mundo.Dimension - 1; y >= 0; y--)
        {
            for (int x = 0; x < mundo.Dimension; x++)
            {
                var celda = new Control();
                celda.CustomMinimumSize = new Vector2(FRAME_SIZE, FRAME_SIZE);
                celda.Name = $"Celda_{x}_{y}";

                // Capa 1: sprite
                var texRect = new TextureRect();
                texRect.Name        = "Sprite";
                texRect.ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize;
                texRect.StretchMode = TextureRect.StretchModeEnum.Scale;
                texRect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                texRect.Texture = CrearFrameTextura(FRAME_VACIO);

                // Capa 2: tinte de estado (semitransparente sobre el sprite)
                var overlay = new ColorRect();
                overlay.Name  = "Overlay";
                overlay.Color = new Color(0f, 0f, 0f, 0f);
                overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

                // Capa 3: borde de 2px en cada lado
                var bordeTop    = CrearLineaBorde("BordeTop",    0,            0,            FRAME_SIZE, 2);
                var bordeBottom = CrearLineaBorde("BordeBottom", 0,            FRAME_SIZE-2, FRAME_SIZE, 2);
                var bordeLeft   = CrearLineaBorde("BordeLeft",   0,            0,            2,          FRAME_SIZE);
                var bordeRight  = CrearLineaBorde("BordeRight",  FRAME_SIZE-2, 0,            2,          FRAME_SIZE);

                celda.AddChild(texRect);
                celda.AddChild(overlay);
                celda.AddChild(bordeTop);
                celda.AddChild(bordeBottom);
                celda.AddChild(bordeLeft);
                celda.AddChild(bordeRight);
                miniMapa.AddChild(celda);
            }
        }
    }

    private ColorRect CrearLineaBorde(string nombre, int ox, int oy, int w, int h)
    {
        var r = new ColorRect();
        r.Name         = nombre;
        r.Color        = new Color(0f, 0f, 0f, 0f);
        r.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        r.OffsetLeft   = ox;
        r.OffsetTop    = oy;
        r.OffsetRight  = ox + w;
        r.OffsetBottom = oy + h;
        return r;
    }

    // =========================================================================
    // MINIMAPA — Actualización por turno
    // Para cada celda actualiza:
    //   • TextureRect "Sprite"  → frame del sprite según conocimiento de la IA
    //   • ColorRect   "Overlay" → tinte de color según estado de inferencia
    //   • ColorRect   "Borde*"  → color del borde (amarillo = posición actual)
    // =========================================================================
    private void ActualizarMiniMapa()
    {
        for (int y = mundo.Dimension - 1; y >= 0; y--)
        {
            for (int x = 0; x < mundo.Dimension; x++)
            {
                var coord   = new Coordenada(x, y);
                var celda   = miniMapa.GetNode<Control>($"Celda_{x}_{y}");
                var texRect = celda.GetNode<TextureRect>("Sprite");
                var overlay = celda.GetNode<ColorRect>("Overlay");

                // - Frame del sprite --------------------─
                int frame = FRAME_VACIO;

                if (inteligencia.Visitados.Contains(coord))
                {
                    frame = mundo.Matriz[x, y].Contenido switch
                    {
                        Elemento.Oro    => FRAME_ORO,
                        Elemento.Hoyo   => FRAME_HOYO,
                        Elemento.Wumpus => FRAME_WUMPUS,
                        _               => FRAME_VACIO
                    };
                }
                else if (inteligencia.BaseConocimiento.TryGetValue(coord, out var estadoFrame))
                {
                    frame = estadoFrame switch
                    {
                        EstadoInferencia.HoyoConfirmado   => FRAME_HOYO,
                        EstadoInferencia.WumpusConfirmado => FRAME_WUMPUS,
                        _                                 => FRAME_VACIO
                    };
                }
                texRect.Texture = CrearFrameTextura(frame);

                // - Color del overlay y del borde --------------
                Color colorOverlay;
                Color colorBorde;

                if (inteligencia.PosicionActual.Equals(coord))
                {
                    // Posición actual: overlay amarillo claro + borde amarillo brillante
                    colorOverlay = new Color(1f, 0.9f, 0f, 0.25f);
                    colorBorde   = new Color(1f, 0.92f, 0f, 1f);
                }
                else if (inteligencia.Visitados.Contains(coord))
                {
                    // Visitada: sin overlay + borde blanco tenue
                    colorOverlay = new Color(0f, 0f, 0f, 0f);
                    colorBorde   = new Color(1f, 1f, 1f, 0.35f);
                }
                else if (inteligencia.BaseConocimiento.TryGetValue(coord, out var estadoColor))
                {
                    // Inferida sin visitar: overlay de color + borde según peligrosidad
                    (colorOverlay, colorBorde) = estadoColor switch
                    {
                        EstadoInferencia.Seguro           => (new Color(0f,   0.8f, 0f,   0.18f), new Color(0.2f, 0.9f, 0.2f, 0.9f)),
                        EstadoInferencia.PosibleHoyo      => (new Color(1f,   0.5f, 0f,   0.2f ), new Color(1f,   0.5f, 0f,   0.8f)),
                        EstadoInferencia.PosibleWumpus    => (new Color(0.9f, 0.1f, 0.1f, 0.2f ), new Color(0.9f, 0.1f, 0.1f, 0.8f)),
                        EstadoInferencia.HoyoConfirmado   => (new Color(1f,   0.2f, 0f,   0.35f), new Color(1f,   0.2f, 0f,   1f  )),
                        EstadoInferencia.WumpusConfirmado => (new Color(0.6f, 0f,   0.9f, 0.35f), new Color(0.7f, 0f,   1f,   1f  )),
                        _                                 => (new Color(0f,   0f,   0f,   0.5f ), new Color(0f,   0f,   0f,   0f  ))
                    };
                }
                else
                {
                    // Completamente desconocida: overlay oscuro + sin borde
                    colorOverlay = new Color(0f, 0f, 0f, 0.55f);
                    colorBorde   = new Color(0f, 0f, 0f, 0f);
                }

                overlay.Color = colorOverlay;

                // Aplicar el mismo color a los cuatro segmentos del borde
                celda.GetNode<ColorRect>("BordeTop").Color    = colorBorde;
                celda.GetNode<ColorRect>("BordeBottom").Color = colorBorde;
                celda.GetNode<ColorRect>("BordeLeft").Color   = colorBorde;
                celda.GetNode<ColorRect>("BordeRight").Color  = colorBorde;
            }
        }
    }

    // =========================================================================
    private AtlasTexture CrearFrameTextura(int frame)
    {
        var atlas    = new AtlasTexture();
        atlas.Atlas  = sheetMinimapa;
        atlas.Region = new Rect2(frame * FRAME_SIZE, 0, FRAME_SIZE, FRAME_SIZE);
        return atlas;
    }

    // =========================================================================
    private void ActualizarLabelHistorial()
    {
        labelHistorial.Clear();
        labelHistorial.AppendText("[color=white]Ruta mental:[/color]");
        var camino = inteligencia.HistorialCamino;
        for (int i = 0; i < camino.Count; i++)
        {
            labelHistorial.AppendText(i == 0
                ? $"\n{camino[i]}"
                : $"\n[color=gray]->[/color] {camino[i]}");
        }
    }

    // =========================================================================
    private void ActualizarHUDPrimeraPersona(Percepcion sensores)
    {
        if (sensores == Percepcion.Ninguna)
        {
            labelSensores.Text = "Entras a una nueva habitación de la cueva...\nTodo está en absoluto silencio y oscuridad profunda.";
            return;
        }

        var desc = new System.Collections.Generic.List<string>
            { "Cruzas el umbral hacia el siguiente cuarto:" };

        if ((sensores & Percepcion.Hedor)      != 0) desc.Add("🤢 Un hedor nauseabundo llena el aire. El Wumpus acecha cerca.");
        if ((sensores & Percepcion.Brisa)      != 0) desc.Add("💨 Una corriente helada golpea tus pies. Hay un foso cerca.");
        if ((sensores & Percepcion.Resplandor) != 0) desc.Add("✨ ¡Un brillo dorado ilumina las paredes! El tesoro está ante ti.");
        if ((sensores & Percepcion.Grito)      != 0) desc.Add("🔊 ¡Un alarido monstruoso retumba desde las profundidades!");

        labelSensores.Text = string.Join("\n", desc);
    }

    // =========================================================================
    private void ManejarFinDeJuego()
    {
        Elemento casillaFinal = mundo.Matriz[inteligencia.PosicionActual.X, inteligencia.PosicionActual.Y].Contenido;

        if (casillaFinal == Elemento.Hoyo || casillaFinal == Elemento.Wumpus)
        {
            labelSensores.Text = "💀 El silencio vuelve a la cueva. Has perecido en la oscuridad.";
            wumpus.Visible = true;
            videoMuerte.Play();
        }
        else if (inteligencia.ObtenerEstadoTieneOro() &&
                 inteligencia.PosicionActual.X == 0   &&
                 inteligencia.PosicionActual.Y == 0)
        {
            labelSensores.Text = "🎉 ¡Misión Cumplida! Has escapado con el Oro.";
        }

        ActualizarMiniMapa();
    }
}