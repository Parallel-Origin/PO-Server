using System.Collections.Generic;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.LowLevel;
using ParallelOrigin.Core.Base.Classes.Pattern.Prototype;
using ParallelOrigin.Core.Base.Classes.Pattern.Registers;
using ParallelOrigin.Core.ECS.Components;
using ParallelOrigin.Core.ECS.Components.Interactions;
using ParallelOriginGameServer.Server.ThirdParty;

namespace ParallelOriginGameServer.Server.Prototyper;

public class RecipePrototyper : Prototyper<Entity>
{
    // Recipes
    private static readonly Recipe FlagRecipe = new(
        new[] { new Ingredient(Types.Wood, 1, 23, 10, true) },
        new[] { new Craftable(Types.Flag, 2, 1) }
    );
    
    // Flag localisation to reduce memory footprint.
    private static readonly Handle<Dictionary<string,short>> FlagRecipeLocalisation = new Dictionary<string,short> {
        { "buildingName", 5 }, { "buildingDescription", 6 }
    }.ToHandle();

    public override void Initialize()
    {
        var world = ServiceLocator.Get<World>();

        // The default flag recipe
        Register(1, () => world.Create<Identity, Mesh, Sprite, Recipe, BuildingRecipe, Localizations>(), (ref Entity entity) =>
        {
            entity.Set(
                new Identity { Type = Types.FlagRecipe, Tag = Tags.Recipe },
                new Mesh { Id = 11, Instantiate = false },
                new Sprite { Id = 2 },
                FlagRecipe,
                new BuildingRecipe { Spot = BuildSpot.Tile, BuildCondition = BuildCondition.FreeSpace, Duration = 30.0f },
                new Localizations
                {
                    LocalizationsHandle = FlagRecipeLocalisation,
                    UniqueLocalizations = Handle<Dictionary<string, string>>.NULL
                }
            );
        });
    }

    public override void AfterInstanced(short typeId, ref Entity clonedInstance)
    {
        base.AfterInstanced(typeId, ref clonedInstance);
        clonedInstance.Add<Prefab>();
    }
}