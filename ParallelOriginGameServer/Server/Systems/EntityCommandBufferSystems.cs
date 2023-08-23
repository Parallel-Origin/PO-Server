using System;
using System.Collections.Generic;
using Arch.CommandBuffer;
using Arch.Core;
using Arch.System;
using ParallelOrigin.Core.Base.Classes.Pattern.Registers;

namespace ParallelOriginGameServer.Server.Systems;

/// <summary>
///     A callback which should just execute some logic.
/// </summary>
public delegate void OnExecute();

/// <summary>
///     A struct which stores a delegate which is used to execute logic once the buffer invokes.
/// </summary>
public struct Buffer
{
    public OnExecute OnExecute;
}

/// <summary>
///     A system which contains a <see cref="EntityCommandRecorder" /> which acts as a buffer for entity modifications.
///     It will play them back during the systems update loop.
/// </summary>
public class CommandBufferSystem : BaseSystem<World, float>
{
    public CommandBufferSystem(World world) : base(world)
    {
        EntityCommandBuffer = new CommandBuffer(world);
    }
    
    /// <summary>
    ///     The command buffer used to play back recorded entity changes.
    /// </summary>
    public CommandBuffer EntityCommandBuffer { get; }

    public override void Update(in float t)
    {
        base.Update(in t);
        
        // Execute the buffered entity commands 
        if (EntityCommandBuffer.Size <= 0) return;
        EntityCommandBuffer.Playback();
    }
}

/// <summary>
///     An system which runs during the start to play back recorded entity modifications.
/// </summary>
public class StartCommandBufferSystem : CommandBufferSystem
{
    public StartCommandBufferSystem(World world) : base(world)
    {
        ServiceLocator.Register(this);
    }
}

/// <summary>
///     An system which runs during the start to play back recorded entity modifications.
/// </summary>
public class EndCommandBufferSystem : CommandBufferSystem
{
    public EndCommandBufferSystem(World world) : base(world)
    {
        ServiceLocator.Register(this);
    }
}