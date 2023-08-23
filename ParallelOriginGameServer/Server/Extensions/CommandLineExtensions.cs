using System;
using System.CommandLine;
using System.CommandLine.Help;
using System.IO;
using Arch.Core;
using Arch.Core.Extensions;
using LiteNetLib;
using Microsoft.Extensions.DependencyInjection;
using ParallelOrigin.Core.Base.Classes;
using ParallelOrigin.Core.Base.Classes.Pattern.Prototype;
using ParallelOrigin.Core.Base.Classes.Pattern.Registers;
using ParallelOrigin.Core.ECS;
using ParallelOrigin.Core.ECS.Components;
using ParallelOrigin.Core.ECS.Components.Transform;
using ParallelOrigin.Core.Network;
using ParallelOriginGameServer.Server.ThirdParty;
using Command = System.CommandLine.Command;
using InventoryCommandSystem = ParallelOriginGameServer.Server.Systems.InventoryCommandSystem;
using TeleportationCommandSystem = ParallelOriginGameServer.Server.Systems.TeleportationCommandSystem;

namespace ParallelOriginGameServer.Server.Extensions;

/// <summary>
///     An extension for the <see cref="System.CommandLine" /> api
/// </summary>
public static class CommandLineExtensions
{
    /// <summary>
    ///     Finds the child command of a command by its name and returns it.
    /// </summary>
    /// <param name="command"></param>
    /// <param name="nameOfChildCommand"></param>
    /// <returns></returns>
    public static Command FindChildCommand(this RootCommand command, string nameOfChildCommand)
    {
        foreach (var childCommand in command.Subcommands)
            if (childCommand.Name.Equals(nameOfChildCommand))
                return childCommand;

        return null;
    }

    /// <summary>
    ///     Creates a teleportation command parser which teleports a set of entities to a specific position by a string command
    ///     <example>/teleport -player Genar Gustav -x 10 -y 10</example>
    /// </summary>
    /// <param name="command"></param>
    /// <param name="world"></param>
    /// <returns></returns>
    public static Command HelpCommand(this RootCommand command, World world)
    {
        // Create help command
        var verbCommand = new Command("/help", "Prints all available commands");
        var commandOption = new Option<string>("-for") { Description = "A command name you wanna see the help for", Arity = ArgumentArity.ZeroOrOne };

        // Add options and create handler to handle this command 
        verbCommand.AddOption(commandOption);
        verbCommand.SetHandler((string commandName) =>
        {
            // Find entity which called that command
            var callerId = Program.CommandUserId.Value;
            var callerEntity = world.GetById(in callerId);
            ref var character = ref callerEntity.Get<Character>();

            // Loged out entities should not be able to receive a chat message
            if (!callerEntity.Has<LogedIn>()) return;

            // Either print root command or the specified command when given
            Command commandToGenerateHelpFor = command;
            if (!string.IsNullOrEmpty(commandName))
                commandToGenerateHelpFor = command.FindChildCommand(commandName);

            // Generate help for the command and print
            var writer = new StringWriter();
            new HelpBuilder(LocalizationResources.Instance).Write(commandToGenerateHelpFor, writer);
            var helpString = writer.ToString();

            // Construct chat message and send to the caller to only show him the help strings. 
            var chatMessageCommand = new ChatMessageCommand
            {
                Channel = 1,
                Date = DateTime.Now,
                Message = helpString,
                SenderUsername = "Parallel Origin"
            };

            // Forward message to player
            var peer = character.Peer.Get();
            Program.Network.Send(peer, ref chatMessageCommand);
        }, commandOption);

        return verbCommand;
    }

    /// <summary>
    ///     Defines a command which sends a chat message to all connected clients.
    /// </summary>
    /// <param name="command"></param>
    /// <param name="world"></param>
    /// <returns></returns>
    public static Command SendMessageCommand(this RootCommand command, World world)
    {
        // Create teleport command
        var verbCommand = new Command("/sendMessage", "Sends a message");
        var nameOption = new Option<string>("-name") { Description = "The sender name, if not defined the sender name will be Parallel Origin", Arity = ArgumentArity.ZeroOrOne };
        var messageOption = new Option<string>("-message") { Description = "The message itself", Arity = ArgumentArity.ExactlyOne };

        // Add options and create handler to handle this command 
        verbCommand.AddOption(nameOption);
        verbCommand.AddOption(messageOption);
        verbCommand.SetHandler((string name, string message) =>
        {
            // Construct chat message and send to the caller to only show him the help strings. 
            var chatMessageCommand = new ChatMessageCommand
            {
                Channel = 1,
                Date = DateTime.Now,
                Message = message,
                SenderUsername = string.IsNullOrEmpty(name) ? "Parallel Origin" : name
            };
            Program.Network.Send(ref chatMessageCommand);
        }, nameOption, messageOption);

        return verbCommand;
    }

    /// <summary>
    ///     Creates a teleportation command parser which teleports a set of entities to a specific position by a string command
    ///     <example>/teleport -player Genar Gustav -x 10 -y 10</example>
    /// </summary>
    /// <param name="command"></param>
    /// <param name="world"></param>
    /// <returns></returns>
    public static Command TeleportationCommand(this RootCommand command, World world)
    {
        // Border
        const float min = -85.0f;
        const float max = 85.0f;

        // Create teleport command
        var verbCommand = new Command("/teleport", "Teleports players around, either randomly or to a certain destination");
        var playerOption = new Option<string[]>("-player")
            { Description = "A list of player names, non active players will be ignored", Arity = ArgumentArity.OneOrMore, AllowMultipleArgumentsPerToken = true };
        var posXOption = new Option<float>("-x") { Description = "The latitude coordinate", Arity = ArgumentArity.ZeroOrOne };
        var posYOption = new Option<float>("-y") { Description = "The longitude coordinate", Arity = ArgumentArity.ZeroOrOne };

        // Add options and create handler to handle this command 
        verbCommand.AddOption(playerOption);
        verbCommand.AddOption(posXOption);
        verbCommand.AddOption(posYOption);
        verbCommand.SetHandler((string[] player, float x, float y) =>
        {
            // Randomize coordinates if not given
            if (x == 0 || !x.Between(min, max)) x = RandomExtensions.GetRandom(min, max);
            if (y == 0 || !y.Between(min, max)) y = RandomExtensions.GetRandom(min, max);

            // Teleport all players to those coordinates 
            var pos = new Vector2d(x, y);
            foreach (var name in player)
            {
                // Create a network & teleportation command being send to the client next frame. 
                var charEntity = world.GetCharacter(name);
                if (!charEntity.IsAlive() || !charEntity.Has<LogedIn>()) continue;

                ref var identity = ref charEntity.Get<Identity>();
                TeleportationCommandSystem.Add(new TeleportationCommand { Entity = new EntityLink(charEntity, identity.Id), Position = pos });
            }
        }, playerOption, posXOption, posYOption);

        return verbCommand;
    }

    /// <summary>
    ///     Creates a teleportation command parser which teleports a set of entities to a specific position by a string command
    ///     <example>/teleport -player Genar Gustav -x 10 -y 10</example>
    /// </summary>
    /// <param name="command"></param>
    /// <param name="world"></param>
    /// <returns></returns>
    public static Command ItemCommand(this RootCommand command, World world)
    {
        // Create teleport command
        var verbCommand = new Command("/item", "Modifies the items of a player");
        var playerOption = new Option<string[]>("-player")
            { Description = "A list of player names, non active players will be ignored", Arity = ArgumentArity.OneOrMore, AllowMultipleArgumentsPerToken = true };
        var typeOption = new Option<string>("-type") { Description = "The item type", Arity = ArgumentArity.ExactlyOne };
        var amountOption = new Option<int>("-amount") { Description = "The amount to add ( negative values possible )", Arity = ArgumentArity.ExactlyOne };

        // Add options and create handler to handle this command 
        verbCommand.AddOption(playerOption);
        verbCommand.AddOption(typeOption);
        verbCommand.AddOption(amountOption);
        verbCommand.SetHandler((string[] player, string type, int amount) =>
        {
            foreach (var name in player)
            {
                // Create a network & teleportation command being send to the client next frame. 
                var charEntity = world.GetCharacter(name);
                if (!charEntity.IsAlive() || !charEntity.Has<LogedIn>()) continue;

                var finalAmount = (uint)Math.Abs(amount);
                var operation = amount >= 0 ? InventoryOperation.ADD : InventoryOperation.SUBSTRACT;
                InventoryCommandSystem.Add(new InventoryCommand(type, finalAmount, charEntity, operation));
            }
        }, playerOption, typeOption, amountOption);

        return verbCommand;
    }
    
    /// <summary>
    ///     Creates a span command parser which spawns a bunch of entities next to you.
    ///     <example>/spawn -type "3:1" -amount 1000</example>
    /// </summary>
    /// <param name="command"></param>
    /// <param name="world"></param>
    /// <returns></returns>
    public static Command SpawnCommand(this RootCommand command, World world)
    {
        // Create teleport command
        var verbCommand = new Command("/spawn", "Spawn entities");
        var typeOption = new Option<string>("-type") { Description = "The type of an entity that will be spawned", Arity = ArgumentArity.ExactlyOne };
        var amountOption = new Option<int>("-amount") { Description = "The amount to spawn", Arity = ArgumentArity.ExactlyOne };

        // Add options and create handler to handle this command 
        verbCommand.AddOption(typeOption);
        verbCommand.AddOption(amountOption);
        verbCommand.SetHandler((string type, int amount) =>
        {
            // Find entity which called that command
            var callerId = Program.CommandUserId.Value;
            var callerEntity = world.GetById(in callerId);
            if (!callerEntity.IsAlive() || !callerEntity.Has<LogedIn>()) return;

            var prototyperHierarchy = ServiceLocator.Get<EntityPrototyperHierarchy>();
            
            // Spawn entities
            ref var transform = ref callerEntity.Get<NetworkTransform>();
            for (var count = 0; count < amount; count++)
            {
                var newEntity = prototyperHierarchy.Clone(type);
                ref var entityTransform = ref newEntity.Get<NetworkTransform>();
                ref var entityRotation = ref newEntity.Get<NetworkRotation>();

                entityTransform.Pos = transform.Pos + RandomExtensions.GetRandomVector2d(0f, 0.01f);
                entityRotation.Value = RandomExtensions.QuaternionStanding();   
            }
        }, typeOption, amountOption);

        return verbCommand;
    }
}