using System.Collections.Generic;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.Core.Utils;
using Arch.LowLevel;
using ParallelOrigin.Core.Base.Classes.Pattern.Prototype;
using ParallelOrigin.Core.Base.Classes.Pattern.Registers;
using ParallelOrigin.Core.ECS.Components;
using ParallelOrigin.Core.ECS.Components.Combat;
using ParallelOrigin.Core.ECS.Components.Environment;
using ParallelOrigin.Core.ECS.Components.Interactions;
using ParallelOrigin.Core.ECS.Components.Items;
using ParallelOrigin.Core.ECS.Components.Transform;
using ParallelOriginGameServer.Server.ThirdParty;

namespace ParallelOriginGameServer.Server.Prototyper;

/// <summary>
///     A prototyper which creates structures.
/// </summary>
public class StructurePrototyper : Prototyper<Entity>
{
    // Flag localisation to reduce memory footprint.
    private static readonly Handle<Dictionary<string,short>> FlagLocalisation = new Dictionary<string,short> {
        { "name", 5 }
    }.ToHandle();
    
    public override void Initialize()
    {
        var world = ServiceLocator.Get<World>();
        var archetype = new ComponentType[]
        {
            typeof(Identity),
            typeof(Structure),
            typeof(Mesh),
            typeof(NetworkTransform),
            typeof(NetworkRotation),
            typeof(BoxCollider),
            typeof(Health),
            typeof(Localizations),
            typeof(OnClickedSpawnPopUp),
            typeof(Saveable),
            typeof(Updateable)
        };
        
        // The default flag
        Register(1, () => world.Create(archetype), (ref Entity entity) =>
            {
                entity.Set(
                    new Identity { Tag = Tags.Structure, Type = Types.Flag },
                    new Mesh { Id = 13, Instantiate = true },
                    new BoxCollider { Width = 0.001f, Height = 0.001f },
                    new Health { CurrentHealth = 100.0f, MaxHealth = 100.0f },
                    new Localizations
                    {
                        LocalizationsHandle = FlagLocalisation,
                        UniqueLocalizations = Handle<Dictionary<string, string>>.NULL
                    },
                    new OnClickedSpawnPopUp { Type = Types.FlagPopup }
                );
        });
    }

    public override void AfterInstanced(short typeId, ref Entity clonedInstance)
    {
        base.AfterInstanced(typeId, ref clonedInstance);
        clonedInstance.Add<Prefab>();
    }
}