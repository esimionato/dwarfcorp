using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using DwarfCorp.GameStates;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;

namespace DwarfCorp
{
    [JsonObject(IsReference = true)]
    public class ParticleEffect
    {
        public List<ParticleEmitter> Emitters { get; set; }

        public ParticleEffect()
        {
            Emitters = new List<ParticleEmitter>();
        }       

        public void Trigger(int num, Vector3 position, Color tint)
        {
            for (int i = 0; i < num; i++)
            {
                Emitters[MathFunctions.Random.Next(Emitters.Count)].Trigger(1, position, tint);
            }
        }        
    }

    /// <summary>
    /// This class manages a set of particle effects, and allows them to be triggered
    /// at locations in 3D space.
    /// </summary>
    [JsonObject(IsReference =  true)]
    public class ParticleManager
    {
        public Dictionary<string, ParticleEffect> Effects { get; set; }

        public ParticleManager()
        {
          
        }

        public void Load(GraphicsDevice Device, ComponentManager Components, Dictionary<string, List<EmitterData>> data)
        {
            Effects.Clear();
            foreach (var effect in data)
            {
                RegisterEffect(Device, Components, effect.Key, effect.Value.ToArray());
            }
        }

        public ParticleManager(GraphicsDevice Device, ComponentManager Components)
        {
            // Todo: Better modding support - make it a list of named emitters.
            Effects = new Dictionary<string, ParticleEffect>();
            Load(Device, Components, FileUtils.LoadJsonFromResolvedPath<Dictionary<string, List<EmitterData>>>(ContentPaths.Particles.particles));
        }

        public void Trigger(string emitter, Vector3 position, Color tint, int num)
        {
            Effects[emitter].Trigger(num, position, tint);
        }

        public void RegisterEffect(GraphicsDevice Device, ComponentManager Components, string name, params EmitterData[] data)
        {
            List<ParticleEmitter> emitters = new List<ParticleEmitter>();

            foreach (EmitterData emitter in data)
            {
                emitters.Add(Components.RootComponent.AddChild(new ParticleEmitter(Device, Components, name, Matrix.Identity, emitter)
                {
                    LightsWithVoxels = false,
                    Tint = Color.White,
                }) as ParticleEmitter);
            }
            Effects[name] = new ParticleEffect()
            {
                Emitters = emitters
            };
        }
    }
}
