using Godot;
using System;

public partial class LB : CharacterBody2D
{
	[Export] int speed = 300;

	public override void _PhysicsProcess(double delta) {
		GD.Print("Hello World");
	}
}
