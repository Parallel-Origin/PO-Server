#define SERVER

using System;
using System.CommandLine;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Arch.Core;
using Arch.Core.Utils;
using Arch.LowLevel;
using CircularBuffer;
using Cysharp.Text;
using LiteNetLib;
using Microsoft.Extensions.Logging;
using ParallelOrigin.Core.Base.Classes;
using ParallelOrigin.Core.Base.Classes.Pattern.Prototype;
using ParallelOrigin.Core.Base.Classes.Pattern.Registers;
using ParallelOrigin.Core.ECS.Components;
using ParallelOrigin.Core.ECS.Components.Interactions;
using ParallelOrigin.Core.ECS.Components.Transform;
using ParallelOrigin.Core.Network;
using ParallelOriginGameServer.Server.Extensions;
using ParallelOriginGameServer.Server.Network;
using ParallelOriginGameServer.Server.Persistence;
using ParallelOriginGameServer.Server.Prototyper;
using ParallelOriginGameServer.Server.Systems;
using ParallelOriginGameServer.Server.ThirdParty;
using ZLogger;

namespace ParallelOriginGameServer;

// TODO : Build operation must become its own entity, a entity to take care of the operation
// TODO : Combat must become its own entity and basically controls the combat process, like attacking, spawning damage commands in ? 
// TODO : QuadTree inline queries ? 
// TODO : Collision/AOI Events and Commands should rather be stored in lists and their own system instead of the ECS ? 
// DODO
internal class Program
{
    ///////////////
    /// Consts
    ///////////////
    /// 
    private const int CappedFps = 60;

    ///////////////
    /// Logging and stuff
    ///////////////
    
    private static ILoggerFactory _factory;

    ///////////////
    /// Command parser
    ///////////////

    // The user-id of the command caller for identify purposes and in case the command is targeting the caller. 
    public static readonly ThreadLocal<long> CommandUserId = new();
    public static RootCommand Command;

    ///////////////
    /// Network & Database
    ///////////////
    
    public static CircularBuffer<ChatMessageCommand> GlobalChatMessages;
    public static ServerNetwork Network;

    ///////////////
    /// Game
    ///////////////
    
    private static DateTime _last = DateTime.Now;
    private static DateTime _current = DateTime.Now;

    private static GameDbContext _gameDbContext;

    private static World _world;
    private static EntityPrototyperHierarchy _entityPrototyper;
    private static CharacterPrototyper _characterPrototyper;
    private static BiomePrototyper _biomePrototyper;
    private static ResourcePrototyper _resourcePrototyper;
    private static ItemPrototyper _itemPrototyper;
    private static ItemOnGroundPrototyper _itemOnGroundPrototyper;
    private static StructurePrototyper _structurePrototyper;
    private static MobPrototyper _mobPrototyper;

    private static RecipePrototyper _recipePrototyper;
    private static PopUpPrototyper _popUpPrototyper;
    private static OptionPrototyper _optionPrototyper;

    private static GeoTiff _biomeGeoTiff;

    /// <summary>
    ///     Returns the logger used for this app.
    /// </summary>
    /// <returns></returns>
    public static ILogger Logger { get; private set; }

    private static void Main(string[] args)
    {

        // Define the logger
        _factory = LoggerFactory.Create(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddZLoggerConsole(options =>
            {
                // Tips: use PrepareUtf8 to achive better performance.
                var prefixFormat = ZString.PrepareUtf8<LogLevel, DateTime>("[{0} - {1}] ");
                options.PrefixFormatter = (writer, info) => prefixFormat.FormatTo(ref writer, info.LogLevel, info.Timestamp.DateTime.ToLocalTime());
            });
            builder.AddZLoggerRollingFile((dt, x) => $"logs/{dt.ToLocalTime():yyyy-MM-dd}_{x:000}.log", x => x.ToLocalTime().Date, 1024, options =>
            {
                // Tips: use PrepareUtf8 to achive better performance.
                var prefixFormat = ZString.PrepareUtf8<LogLevel, DateTime>("[{0} - {1}] ");
                options.PrefixFormatter = (writer, info) => prefixFormat.FormatTo(ref writer, info.LogLevel, info.Timestamp.DateTime.ToLocalTime());
            });
        });
        Logger = _factory.CreateLogger<Program>();

        // Hopefully log crashes 
        AppDomain.CurrentDomain.UnhandledException += OnException;
        TaskScheduler.UnobservedTaskException += OnTaskException;

        // Create world
        _world = World.Create();
        ServiceLocator.Register(_world);
        ComponentRegistry.Add(new ComponentType( ComponentRegistry.Size+1, typeof(Toggle<Clicked>), Unsafe.SizeOf<Toggle<Clicked>>(), false));

        ///////////////////////////////////////////
        /// GeoTiff files
        ///////////////////////////////////////////

        // Relative path, requires to be next to the exe.
        _biomeGeoTiff = new GeoTiff("pnv_biome.type_biome00k_c_1km_s0..0cm_2000..2017_v0.1.tif"); // Only has 1 band, acess via "1!

        ///////////////////////////////////////////
        /// Command parser for string commands
        ///////////////////////////////////////////

        Command = new RootCommand("ParallelOrigin");
        Command.Add(Command.HelpCommand(_world));
        Command.Add(Command.SendMessageCommand(_world));
        Command.Add(Command.TeleportationCommand(_world));
        Command.Add(Command.ItemCommand(_world));
        Command.Add(Command.SpawnCommand(_world));

        ///////////////
        /// Server
        ///////////////

        GlobalChatMessages = new CircularBuffer<ChatMessageCommand>(50);

        // Creating server
        Network = new ServerNetwork
        {
            Ip = "127.0.0.1",
            Port = 9050
        };
        Network.Start();
        ServiceLocator.Register(Network);

        Network.OnReceive<LoginCommand>(Network.Login, () => new LoginCommand());
        Network.OnReceive<RegisterCommand>(Network.Register, () => new RegisterCommand());
        
        Network.OnReceive<EntityCommand>((cmd, _) => EntityCommandSystem.Add(cmd), () => new EntityCommand());
        Network.OnReceive<ClickCommand>((cmd, _) => ClickedCommandSystem.Add(cmd), () => new ClickCommand());                 // Click on entity
        Network.OnReceive<DoubleClickCommand>((cmd, _) => DoubleClickedCommandSystem.Add(cmd), () => new DoubleClickCommand());    // Double click to move

        Network.OnReceive<ChatMessageCommand>((cmd, _) => ChatCommandSystem.Add(cmd), () => new ChatMessageCommand());   // Chat
        Network.OnReceive<BuildCommand>((cmd, _) => BuildCommandSystem.Add(cmd), () => new BuildCommand());                 // Build command to start build
        Network.OnReceive<PickupCommand>((cmd, _) => PickupCommandSystem.Add(cmd), () => new PickupCommand());                     // Picking up items

        Network.OnLogin += _world.InitializeCharacter;
        Network.OnLogin += Network.SendLoginResponse;
        Network.OnLogin += Network.SendGlobalChat;

        Network.OnRegister += _world.CreateCharacter;
        Network.OnRegister += Network.SendLoginResponse;

        Network.OnLogout += _world.DeinitializeCharacter;

        Network.OnConnectionRequest += request => Logger.ZLogInformation("Connection request from {0}", request.RemoteEndPoint.Address);
        Network.OnDisconnected += (peer, info) => Logger.ZLogInformation("{0} disconnected because of {1}", peer.EndPoint.Address, info.Reason);
        Network.OnDisconnected += Network.Logout;

        Logger.ZLogInformation("Server started");


        ///////////////
        /// Database
        ///////////////
        
        _gameDbContext = new GameDbContext(true);
        _gameDbContext.Database.EnsureCreated();
        _gameDbContext.SaveChanges();
        ServiceLocator.Register(_gameDbContext);
        
        var accounts = _gameDbContext.Accounts.ToList();
        Logger.ZLogInformation("Loaded Accounts");

        ///////////////
        /// Prototypers
        ///////////////

        _entityPrototyper = new EntityPrototyperHierarchy();
        _characterPrototyper = new CharacterPrototyper();
        _biomePrototyper = new BiomePrototyper();
        _resourcePrototyper = new ResourcePrototyper();
        _itemPrototyper = new ItemPrototyper();
        _itemOnGroundPrototyper = new ItemOnGroundPrototyper();
        _structurePrototyper = new StructurePrototyper();
        _mobPrototyper = new MobPrototyper();

        _recipePrototyper = new RecipePrototyper();
        _popUpPrototyper = new PopUpPrototyper();
        _optionPrototyper = new OptionPrototyper();

        _entityPrototyper.Register("biome", _biomePrototyper);
        _entityPrototyper.Register("1", _characterPrototyper);
        _entityPrototyper.Register("2", _resourcePrototyper);
        _entityPrototyper.Register("3", _itemPrototyper);
        _entityPrototyper.Register("4", _structurePrototyper);
        _entityPrototyper.Register("5", _mobPrototyper);
        _entityPrototyper.Register("6", _itemOnGroundPrototyper);

        _entityPrototyper.Register("recipe", _recipePrototyper);

        _entityPrototyper.Register("ui_popup", _popUpPrototyper);
        _entityPrototyper.Register("ui_option", _optionPrototyper);
        ServiceLocator.Register(_entityPrototyper);

        Logger.ZLogInformation("Initialized Prototypers");

        ///////////////
        /// World setup
        ///////////////

        // Degree of parallelism needs to be smaller for our linux server becaue hes less powerfull
        var systems = new Arch.System.Group<float>(
            
            // Reactive Systems
            new ReactiveGroup(_world),
            new StartCommandBufferSystem(_world),
            new InitialisationSystem(_world),

            // Commands, triggering logic between entities mostly 
            new CommandGroup(_world),
            
            // UI & Interactin
            new InteractionGroup(_world),

            // Environment group ( Chunks, terrain generation... )
            new EnvironmentGroup(_world, _entityPrototyper, _biomeGeoTiff),

            // Behaviour like ai and animations
            new BehaviourGroup(_world),

            // Movement & Combat
            new MovementGroup(_world),
            new CombatGroup(_world),

            // Phyics  & Activity group
            new PhysicsGroup(_world),
            new ActivityGroup(_world, _entityPrototyper),

            // Networking group & Network Commands
            new NetworkingGroup(_world, _entityPrototyper, Network),

            // Database group
            new DatabaseGroup(_world, _gameDbContext),
            new IntervallGroup(10.0f, new DebugSystem(_world, Network)),

            // End of the frame
            new ClearTrackedAnimationsSystem(_world),
            new DestroySystem(_world)
        );
        systems.Initialize();

        // Spawn in players
        for (var index = 0; index < accounts.Count; index++)
        {
            // Get account & character
            var acc = accounts[index]; 
            acc.ToEcs();
        }

        Logger.ZLogInformation("Initialized World");

        ///////////////
        /// Game Loop
        ///////////////
        
        while (true)
        {
            _current = DateTime.Now;
            var deltaTime = (_current.Ticks - _last.Ticks) / 10000000f;

            Network.Update();
            systems.BeforeUpdate(deltaTime);
            systems.Update(deltaTime);
            systems.AfterUpdate(deltaTime);
            Network.Manager.TriggerUpdate();
            _last = _current;

            var sleepDuration = _current.Millisecond + 1000 / CappedFps - DateTime.Now.Millisecond;
            if (sleepDuration >= 0) Thread.Sleep(sleepDuration);
        }
    }

    private static void OnException(object sender, UnhandledExceptionEventArgs args)
    {
        var e = (Exception)args.ExceptionObject;
        Logger.ZLogError("[ERROR] {0}", e.Message);
        Logger.ZLogError("[ERROR] {0}", e.StackTrace);
    }

    private static void OnTaskException(object sender, UnobservedTaskExceptionEventArgs args)
    {
        var e = (Exception)args.Exception;
        Logger.ZLogError("[ERROR] {0}", e.Message);
        Logger.ZLogError("[ERROR] {0}", e.StackTrace);
    }
}

/// <summary>
///     Possible log formats
/// </summary>
public static class Logs
{
    // Single action by an entity... 2 the one that caused the action 
    public const string SingleAction = "[{0}] | [{1}] involved {2}";

    // A normal action which involes 2 entities... 2 the one that caused the action and 3 the one that received the action
    public const string Action = "[{0}] | [{1}] involved {2} and {3}";

    // A message which was send
    public const string Message = "[Message send] | [{0}] | [{1}] by {2}";

    public const string Login = "[Login] | [{0}] with {1} and {2} from {3}";

    // Chunks
    public const string Chunk = "[Chunk] | [{0}] {1}/{2}/{3}";
    public const string ChunkSwitch = "[Chunk] | [SWITCH] {0} left chunk {1}/{2} and entered chunk {3}/{4}";
    public const string Aoi = "[AOI] | [{0}] {1} entities the aoi of {2}";

    // Terrain generation
    public const string Biome = "[Biome] | Chunk [{0}] at {1}/{2} was marked with biomecode {3}";
    public const string Spawning = "[Spawning] | Layer [{0}] spawned {1} entities in chunk {2}";

    // Recipe
    public const string Recipe = "[Recipe] | [{0}] | [{1}] was used by {2}";

    // Combat 
    public const string Damage = "[DAMAGE] | [{0}] | {0} damage applied to {2} by {3}";
    public const string Health = "[HEALTH] | {0} has {1} HP left";
    public const string Killed = "[HEALTH] | {0} was killed by {1}";

    public const string Gamestate = "[Gamestate] | [{0}]";
}

/// <summary>
///     Possible log errors
/// </summary>
public static class LogStatus
{
    public const string Sucessfull = "Sucessfull";
    public const string Validating = "Validating";
    public const string Failed = "Failed";

    public const string EntityNotAlive = "Entity is not alive";
    public const string BadUsername = "Bad username";
    public const string BadPassword = "Bad password";
}