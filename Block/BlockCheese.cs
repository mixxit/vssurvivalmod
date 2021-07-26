﻿using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockCheese : Block
    {
        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            InteractionHelpYOffset = 0.375f;

            interactions = ObjectCacheUtil.GetOrCreate(api, "cheeseInteractions-", () =>
            {
                List<ItemStack> knifeStacks = new List<ItemStack>();

                foreach (CollectibleObject obj in api.World.Items)
                {
                    if (obj.Tool == EnumTool.Knife || obj.Tool == EnumTool.Sword)
                    {
                        knifeStacks.Add(new ItemStack(obj));
                    }
                }

                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-cheese-cut",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = knifeStacks.ToArray(),
                        GetMatchingStacks = (wi, bs, es) => {
                            BECheese bec = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BECheese;
                            if (bec != null && bec.SlicesLeft > 1)
                            {
                                return wi.Itemstacks;
                            }
                            return null;
                        }
                    }
                };
            });
        }

        public override void GetDecal(IWorldAccessor world, BlockPos pos, ITexPositionSource decalTexSource, ref MeshData decalModelData, ref MeshData blockModelData)
        {
            base.GetDecal(world, pos, decalTexSource, ref decalModelData, ref blockModelData);
        }

        public override void OnDecalTesselation(IWorldAccessor world, MeshData decalMesh, BlockPos pos)
        {
            base.OnDecalTesselation(world, decalMesh, pos);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            BECheese bec = world.BlockAccessor.GetBlockEntity(pos) as BECheese;
            if (bec != null) return bec.Inventory[0].Itemstack;

            return base.OnPickBlock(world, pos);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            EnumTool? tool = byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible.Tool;
            if (tool == EnumTool.Knife || tool == EnumTool.Sword)
            {
                BECheese bec = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BECheese;

                if (bec.Inventory[0].Itemstack?.Collectible.Variant["type"] == "waxedcheddar")
                {
                    bec.Inventory[0].Itemstack = new ItemStack(api.World.GetItem(bec.Inventory[0].Itemstack?.Collectible.CodeWithVariant("type", "cheddar")));
                    bec.Inventory[0].MarkDirty();
                    bec.MarkDirty(true);
                    return true;
                }

                ItemStack stack = bec?.TakeSlice();
                if (stack != null)
                {
                    if (!byPlayer.InventoryManager.TryGiveItemstack(stack, true))
                    {
                        world.SpawnItemEntity(stack, blockSel.Position.ToVec3d().Add(0.5, 0.5, 0.5));
                    }
                }

                return true;
            } else
            {
                BECheese bec = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BECheese;
                var stack = bec.Inventory[0].Itemstack;
                if (stack != null)
                {
                    if (!byPlayer.InventoryManager.TryGiveItemstack(stack, true))
                    {
                        world.SpawnItemEntity(stack, blockSel.Position.ToVec3d().Add(0.5, 0.5, 0.5));
                    }
                }

                world.BlockAccessor.SetBlock(0, blockSel.Position);
                return true;
            }
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }
    }
}
