namespace SpaceBetweenUs
{
    /// <summary>The three materials locked inside the asteroids.</summary>
    public enum MaterialType
    {
        Rock,    // gold  - shell  - builds the island's ground
        Ice,     // blue  - inside - melts into water
        Crystal  // violet - core  - grows the lotus (life)
    }

    public enum Role
    {
        Builder,
        Destroyer
    }
}
