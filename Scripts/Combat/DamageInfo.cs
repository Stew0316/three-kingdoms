using Godot;

public readonly record struct DamageInfo(
    Node2D Attacker,
    int Amount,
    Vector2 KnockbackDirection
);