using System;
using System.Runtime.CompilerServices;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.System;
using LiteNetLib;
using LiteNetLib.Utils;
using ParallelOrigin.Core.Base.Classes.Pattern.Prototype;
using ParallelOrigin.Core.ECS;
using ParallelOrigin.Core.ECS.Components;
using ParallelOrigin.Core.ECS.Components.Combat;
using ParallelOrigin.Core.ECS.Components.Environment;
using ParallelOrigin.Core.ECS.Components.Interactions;
using ParallelOrigin.Core.ECS.Components.Items;
using ParallelOrigin.Core.ECS.Components.Transform;
using ParallelOrigin.Core.Network;
using ParallelOriginGameServer.Server.Extensions;
using ParallelOriginGameServer.Server.Network;
using ParallelOriginGameServer.Server.ThirdParty;
using ZLogger;

namespace ParallelOriginGameServer.Server.Systems;

/// <summary>
///     A system group which controlls all networking.
///     Also processes network commands, those will be processed here instead of the normal command group.
/// </summary>
public sealed class NetworkingGroup : Group<float>
{
    public NetworkingGroup(World world, EntityPrototyperHierarchy prototyperHierarchy, ServerNetwork network) : base(
        
        // Networking character info to his client
        new CharacterNetworkSystem(world, network, prototyperHierarchy),
        new PopUpNetworkSystem(world, network)
    )
    {
    }
}

/// <summary>
///     An system which reacts to added <see cref="Character" />'s to send them to the client for showing up properly.
/// </summary>
public sealed partial class CharacterNetworkSystem : BaseSystem<World,float>
{
    private readonly ServerNetwork _network;
    public readonly EntityPrototyperHierarchy Prototyper;

    public CharacterNetworkSystem(World world, ServerNetwork network, EntityPrototyperHierarchy prototyperHierarchy) : base(world)
    {
        this._network = network;
        this.Prototyper = prototyperHierarchy;
    }

    [Query]
    [All(typeof(LogedIn), typeof(Toggle<DirtyNetworkTransform>))]
    private void SendTransform(ref Identity identity, ref Character character, ref NetworkTransform transform, ref NetworkRotation rotation, ref Toggle<DirtyNetworkTransform> toggle)
    {
        if (!toggle.Enabled) return;
        
        var entityCommand = new EntityCommand<NetworkTransform, NetworkRotation>();
        entityCommand.Command = new EntityCommand { Id = identity.Id, Type = string.Empty, Opcode = EntityOpCode.Set };
        entityCommand.T1Component = transform;
        entityCommand.T2Component = rotation;

        _network.Send(character.Peer.Get(), ref entityCommand, DeliveryMethod.Sequenced);
    }
    
    [Query]
    [All(typeof(LogedIn), typeof(DirtyNetworkHealth))]
    private void SendHealth(ref Identity identity, ref Character character, ref Health health)
    {
        var entityCommand = new EntityCommand<Health>();
        entityCommand.Command = new EntityCommand { Id = identity.Id, Type = string.Empty, Opcode = EntityOpCode.Set };
        entityCommand.T1Component = health;

        _network.Send(character.Peer.Get(), ref entityCommand, DeliveryMethod.Sequenced);
    }
    
    
    [Query]
    [All(typeof(LogedIn))]
    private void SendAnim(ref Identity identity, ref Character character, ref Animation animation)
    {
        var boolParams = animation.BoolParams.Get();
        var triggers = animation.Triggers.Get();
        
        // Skip entities not having any set bool params or triggers. 
        if (boolParams.Tracked.Count <= 0 && triggers.Count <= 0) return;
        SendAnimationCommand(ref character, ref identity, ref animation);
    }

    /// <summary>
    /// Creates an animation command which contains a list of changed bool params and set triggers and writes them to the network stream. 
    /// </summary>
    /// <param name="character"></param>
    /// <param name="entityAnimation"></param>
    /// <param name="entityIdentity"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SendAnimationCommand(ref Character character, ref Identity entityIdentity, ref Animation entityAnimation)
    {
        var peer = character.Peer.Get();
        var boolParams = entityAnimation.BoolParams.Get();
        var triggers = entityAnimation.Triggers.Get();
        
        // Write changed bool params to the batch
        for (var index = 0; index < boolParams.Tracked.Count; index++) {

            var kvp = boolParams.Tracked[index];
            
            var boolParamsCommand = new AnimationParamCommand { EntityLink = new EntityLink(entityIdentity.Id), BoolName = kvp.Key, Activated = kvp.Val };
            var stateFull = new Statefull<AnimationParamCommand> { State = kvp.State, Item = boolParamsCommand }; ;
            _network.Send(peer, ref stateFull, DeliveryMethod.ReliableUnordered);
        }
        
        // Write new triggers to the batch 
        for (var index = 0; index < triggers.Count; index++) {

            var kvp = triggers[index];

            var trigger = new AnimationTriggerCommand { EntityLink = new EntityLink(entityIdentity.Id), TriggerName = kvp };
            _network.Send(peer, ref trigger, DeliveryMethod.ReliableUnordered);
        }
    }
}


/// <summary>
///     A system iterating over newly <see cref="Created" /> <see cref="Popup" />'s to send them to client they belong to.
/// </summary>
public sealed partial class PopUpNetworkSystem : BaseSystem<World,float>
{
    private readonly ServerNetwork _network;

    public PopUpNetworkSystem(World world, ServerNetwork network) : base(world)
    {
        this._network = network;
    }
    
    [Query]
    [All(typeof(Created)), None(typeof(Prefab))]
    private void SendPopup(ref Identity identity, ref Popup popup, ref Mesh mesh, ref Localizations localizations, ref Parent parent)
    {
        // The peer of the owner 
        ref var ownerCharacter = ref popup.Owner.Entity.Entity.Get<Character>();
        var peer = ownerCharacter.Peer.Get();

        // Construct a packet from the popup entity
        var command = new EntityCommand<Identity, Popup, Mesh, Localizations, Parent>();
        command.Command = new EntityCommand { Id = identity.Id, Type = identity.Type, Opcode = EntityOpCode.Create };
        command.T1Component = identity;
        command.T2Component = popup;
        command.T3Component = mesh;
        command.T4Component = localizations;
        command.T5Component = parent;

        _network.Send(peer, ref command);
    }
    
    [Query]
    [All(typeof(Destroy)),None(typeof(Prefab))]
    private void SendRemovePopup(ref Identity identity, ref Popup popup, ref Parent parent)
    {
        // Get character who opened this popup
        var characterEntity = (Entity) popup.Owner.Entity;
        ref var character = ref characterEntity.Get<Character>();
        var peer = character.Peer.Get();

        // Destroy all childs
        foreach (var child in parent.Children)
        {
            var childEntity = (Entity)child.Entity;
            ref var childIdentity = ref childEntity.Get<Identity>();

            var childCommand = new EntityCommand { Id = childIdentity.Id, Type = string.Empty, Opcode = EntityOpCode.Delete };
            _network.Send(peer, ref childCommand);
        }

        // Destroy popup itself
        var command = new EntityCommand { Id = identity.Id, Type = string.Empty, Opcode = EntityOpCode.Delete };
        _network.Send(peer, ref command);
    }
    
    [Query]
    [All(typeof(Created), typeof(Option)), None(typeof(Prefab))]
    private void SendOption(ref Identity identity, ref Mesh mesh, ref Localizations localizations, ref Child child)
    {
        // Get popup and character
        ref var popup = ref child.Parent.Entity.Entity.Get<Popup>();
        ref var ownerCharacter = ref popup.Owner.Entity.Entity.Get<Character>();
        var peer = ownerCharacter.Peer.Get();

        // Construct a packet from the popup entity
        var command = new EntityCommand<Identity, Mesh, Localizations, Child>();
        command.Command = new EntityCommand { Id = identity.Id, Type = identity.Type, Opcode = EntityOpCode.Create };
        command.T1Component = identity;
        command.T2Component = mesh;
        command.T3Component = localizations;
        command.T4Component = child;

        _network.Send(peer, ref command);
    }
}
