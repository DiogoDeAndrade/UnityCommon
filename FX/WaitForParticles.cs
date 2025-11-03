using UnityEngine;

public class WaitForParticles : CustomYieldInstruction
{
    private readonly ParticleSystem _particleSystem;

    public WaitForParticles(ParticleSystem particleSystem)
    {
        _particleSystem = particleSystem;
    }

    public override bool keepWaiting
    {
        get
        {
            if (_particleSystem == null)
                return false; // particle system was destroyed

            // Has the system started and is it still alive?
            return (_particleSystem.isPlaying) && (_particleSystem.particleCount > 0) || (_particleSystem.time == 0f);
        }
    }
}
