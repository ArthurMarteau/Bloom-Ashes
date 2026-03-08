// Directions d'animation disponibles pour les unités
// Les directions Est (NE, E, SE) sont rendues par miroir horizontal des directions Ouest
public enum AnimationDirection
{
    North,      // N
    NorthWest,  // NW
    West,       // W
    SouthWest,  // SW
    South,      // S
    NorthEast,  // NE (miroir de NW)
    East,       // E (miroir de W)
    SouthEast   // SE (miroir de SW)
}
