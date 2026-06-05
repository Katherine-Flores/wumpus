using System;

public enum Elemento { Vacio, Hoyo, Wumpus, Oro }

[Flags]
public enum Percepcion { Ninguna = 0, Brisa = 1, Hedor = 2, Resplandor = 4, Grito = 8 }

public enum EstadoInferencia { Desconocido, Seguro, PosibleHoyo, PosibleWumpus, HoyoConfirmado, WumpusConfirmado }

public struct Coordenada
{
    public int X;
    public int Y;
    public Coordenada(int x, int y) { X = x; Y = y; }
    
    public override bool Equals(object obj) => obj is Coordenada c && c.X == X && c.Y == Y;
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public override string ToString() => $"[{X},{Y}]";
}
