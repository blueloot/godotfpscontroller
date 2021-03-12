// using Godot;
// using System;

// public class MovingPlatform : KinematicBody
// {

//     // using animation player at the moment until later

//     [Export] private Vector3[] Station;   
//     [Export] private float IdleTime = 1f; 
//     [Export] private bool RunOnce = true; 
//     [Export] private bool Looped = false; 
//     [Export] private float Speed = 3.0f;

//     KinematicBody Platform;

//     public override void _Ready()
//     {
//         Platform = GetNode<KinematicBody>(this.GetPath());
//         Station = (Station is null) ? new Vector3[0] : Station;

//         if (Station.Length > 0)
//         {
            


//         }
//     }
// }
