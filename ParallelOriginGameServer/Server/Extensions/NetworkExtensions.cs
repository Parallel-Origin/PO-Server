using System.Collections.Generic;
using LiteNetLib;
using ParallelOrigin.Core.Base.Classes.Pattern.Registers;
using ParallelOrigin.Core.Network;
using ParallelOriginGameServer.Server.Network;
using ParallelOriginGameServer.Server.Persistence;
using ZLogger;
using Character = ParallelOrigin.Core.ECS.Components.Character;

namespace ParallelOriginGameServer.Server.Extensions;

/// <summary>
///     An extension for the networking api used for client and server.
/// </summary>
public static class NetworkExtensions
{
    /// <summary>
    ///     A dictionary which stores the loged in account to the active connection.
    /// </summary>
    public static IDictionary<NetPeer, Account> LogedInAccounts { get; set; } = new Dictionary<NetPeer, Account>(64);

    /// <summary>
    ///     Logs in an player and sends him all initial data.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="peer"></param>
    /// <param name="command"></param>
    public static void Login(this ServerNetwork network, LoginCommand command, NetPeer peer)
    {
        Program.Logger.ZLogInformation(Logs.Login, LogStatus.Validating, command.Username, command.Password, peer);

        var context = ServiceLocator.Get<GameDbContext>();

        // Check if account exists, otherwhise throw error
        var existsTask = context.AccountExists(command.Username);
        existsTask.Wait();

        if (!existsTask.Result)
        {
            var errorCommand = new ErrorCommand { Error = Error.BadUsername };
            network.Send(peer, ref errorCommand);

            Program.Logger.ZLogInformation(Logs.Login, LogStatus.BadUsername, command.Username, command.Password, peer);
            return;
        }

        // Check if password is correct, otherwhise throw error
        var accountTask = context.Login(command.Username, command.Password);
        accountTask.Wait();

        if (accountTask.Result != null)
        {
            // Disconnect the new log in if a user tries to login multiple times
            if (LogedInAccounts.ContainsKey(peer)) peer.Disconnect();

            network.OnLogin(peer, accountTask.Result);
            LogedInAccounts[peer] = accountTask.Result;
        }
        else
        {
            var errorCommand = new ErrorCommand { Error = Error.BadPassword };
            network.Send(peer, ref errorCommand);

            Program.Logger.ZLogInformation(Logs.Login, LogStatus.BadPassword, command.Username, command.Password, peer);
            return;
        }

        Program.Logger.ZLogInformation(Logs.Login, LogStatus.Sucessfull, command.Username, command.Password, peer);
    }

    /// <summary>
    ///     Registers an player and sends him all initial data.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="peer"></param>
    /// <param name="command"></param>
    public static void Register(this ServerNetwork network, RegisterCommand command, NetPeer peer)
    {
        var context = ServiceLocator.Get<GameDbContext>();

        // Check if account exists, otherwhise throw error
        var existsTask = context.AccountExists(command.Username);
        existsTask.Wait();

        if (!existsTask.Result)
        {
            var errorCommand = new ErrorCommand { Error = Error.UsernameTaken };
            network.Send(peer, ref errorCommand);
        }

        // Check if account exists, otherwhise throw error
        var emailExistsTask = context.EmailExists(command.Email);
        emailExistsTask.Wait();

        if (!emailExistsTask.Result)
        {
            var errorCommand = new ErrorCommand { Error = Error.EmailTaken };
            network.Send(peer, ref errorCommand);
        }

        var account = context.Register(command.Username, command.Password, command.Email, command.Gender);
        network.OnRegister(peer, account);
    }

    /// <summary>
    ///     Logs out an certain user.
    ///     Mostly used with disconnects event.
    /// </summary>
    /// <param name="network"></param>
    /// <param name="peer"></param>
    /// <param name="disconnectInfo"></param>
    public static void Logout(this ServerNetwork network, NetPeer peer, DisconnectInfo disconnectInfo)
    {
        if (!LogedInAccounts.ContainsKey(peer))
            return;

        var acc = LogedInAccounts[peer];
        network.OnLogout(peer, acc);

        if (LogedInAccounts.ContainsKey(peer))
            LogedInAccounts.Remove(peer);

        Program.Logger.ZLogInformation(Logs.SingleAction, "Logout", LogStatus.Sucessfull, acc.Username);
    }

    /// <summary>
    ///     Sends an login response to an client with all his login and account informations.
    /// </summary>
    /// <param name="peer"></param>
    /// <param name="account"></param>
    public static void SendLoginResponse(this ServerNetwork network, NetPeer peer, Account account)
    {
        // Login player and send him the map. 
        var character = new Character { Name = account.Username, Password = account.Password, Email = account.Email };
        var loginResponse = new LoginResponse { Character = character };
        network.Send(peer, ref loginResponse);
    }

    /// <summary>
    ///     Sends the whole global chat to a user once he loged in.
    /// </summary>
    /// <param name="network"></param>
    /// <param name="peer"></param>
    /// <param name="account"></param>
    public static void SendGlobalChat(this ServerNetwork network, NetPeer peer, Account account)
    {
        var globalChats = Program.GlobalChatMessages;
        var batchCommand = new BatchCommand<ChatMessageCommand>();
        batchCommand.Size = globalChats.Size;
        batchCommand.Data = new ChatMessageCommand[globalChats.Size];

        for (var index = 0; index < globalChats.Size; index++)
        {
            var message = globalChats[index];
            batchCommand.Data[index] = message;
        }

        network.Send(peer, ref batchCommand);
    }
}