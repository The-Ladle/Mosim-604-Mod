using UnityEngine;

namespace Prefabs.Reefscape.Robots.Mods._604B
{
    [CreateAssetMenu(fileName = "Setpoint", menuName = "Robot/Quixshot Setpoint", order = 0)]
    public class QuixshotSetpoint : ScriptableObject
    {
        [Tooltip("Degrees")] public float intakePivotAngle;
        [Tooltip("Degrees")] public float shooterPivotAngle;
        [Tooltip("Newtons")] public float releaseForce;
    }
}