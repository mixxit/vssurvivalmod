﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockBloomery : Block
    {
        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "bloomeryBlockInteractions", () =>
            {
                List<ItemStack> heatableStacklist = new List<ItemStack>();
                List<ItemStack> fuelStacklist = new List<ItemStack>();
                List<ItemStack> canIgniteStacks = new List<ItemStack>();

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    if (obj.CombustibleProps?.SmeltedStack != null && obj.CombustibleProps.MeltingPoint < 1500)
                    {
                        List<ItemStack> stacks = obj.GetHandBookStacks(capi);
                        if (stacks != null) heatableStacklist.AddRange(stacks);
                    } else
                    {
                        if (obj.CombustibleProps?.BurnTemperature >= 1200 && obj.CombustibleProps.BurnDuration > 30)
                        {
                            List<ItemStack> stacks = obj.GetHandBookStacks(capi);
                            if (stacks != null) fuelStacklist.AddRange(stacks);
                        }
                    }

                    if (obj is Block && (obj as Block).HasBehavior<BlockBehaviorCanIgnite>())
                    {
                        List<ItemStack> stacks = obj.GetHandBookStacks(capi);
                        if (stacks != null) canIgniteStacks.AddRange(stacks);
                    }
                }

                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-bloomery-heatable",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = heatableStacklist.ToArray(),
                        GetMatchingStacks = getMatchingStacks
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-bloomery-heatablex4",
                        HotKeyCode = "sneak",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = heatableStacklist.ToArray(),
                        GetMatchingStacks = getMatchingStacks
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-bloomery-fuel",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = fuelStacklist.ToArray(),
                        GetMatchingStacks = getMatchingStacks
                    }, 
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-bloomery-ignite",
                        HotKeyCode = "sneak",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = canIgniteStacks.ToArray(),
                        GetMatchingStacks = (wi, bs, es) => {
                            BlockEntityBloomery beb = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityBloomery;
                            if (beb!= null && beb.CanIgnite() == true && !beb.IsBurning && api.World.BlockAccessor.GetBlock(bs.Position.UpCopy()).Code.Path.Contains("bloomerychimney"))
                            {
                                return wi.Itemstacks;
                            }
                            return null;
                        }
                    }
                };
            });
        }

        private ItemStack[] getMatchingStacks(WorldInteraction wi, BlockSelection blockSelection, EntitySelection entitySelection)
        {
            BlockEntityBloomery beb = api.World.BlockAccessor.GetBlockEntity(blockSelection.Position) as BlockEntityBloomery;
            if (beb == null || wi.Itemstacks.Length == 0) return null;

            List<ItemStack> matchStacks = new List<ItemStack>();
            foreach (ItemStack stack in wi.Itemstacks)
            {
                if (beb.CanAdd(stack)) matchStacks.Add(stack);
            }

            return matchStacks.ToArray();
        }

        public override EnumIgniteState OnTryIgniteBlock(EntityAgent byEntity, BlockPos pos, float secondsIgniting)
        {
            BlockEntityBloomery beb = byEntity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityBloomery;
            if (!beb.CanIgnite()) return EnumIgniteState.NotIgnitablePreventDefault;

            return secondsIgniting > 4 ? EnumIgniteState.IgniteNow : EnumIgniteState.Ignitable;
        }

        public override void OnTryIgniteBlockOver(EntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;

            BlockEntityBloomery beb = byEntity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityBloomery;
            beb?.TryIgnite();
        }


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemStack hotbarstack = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack;

            if (hotbarstack != null && hotbarstack.Class == EnumItemClass.Block && hotbarstack.Collectible.Code.Path.StartsWith("bloomerychimney"))
            {
                Block aboveBlock = world.BlockAccessor.GetBlock(blockSel.Position.UpCopy());
                if (aboveBlock.IsReplacableBy(hotbarstack.Block))
                {
                    hotbarstack.Block.DoPlaceBlock(world, byPlayer, new BlockSelection() { Position = blockSel.Position.UpCopy(), Face = BlockFacing.UP }, hotbarstack);
                    world.PlaySoundAt(Sounds?.Place, blockSel.Position.X, blockSel.Position.Y + 1, blockSel.Position.Z, byPlayer, true, 16, 1);

                    if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                    {
                        byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
                    }
                }


                
                return true;
            }

            BlockEntityBloomery beb = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityBloomery;
            if (beb != null)
            {
                
                if (hotbarstack == null) return true;
                if (beb.TryAdd(byPlayer, byPlayer.Entity.Controls.Sneak ? 5 : 1))
                {
                    if (world.Side == EnumAppSide.Client) (byPlayer as IClientPlayer).TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                }
                
            }

            return true;
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            Block aboveBlock = world.BlockAccessor.GetBlock(pos.UpCopy());
            if (aboveBlock.Code.Path == "bloomerychimney")
            {
                aboveBlock.OnBlockBroken(world, pos.UpCopy(), byPlayer, dropQuantityMultiplier);
            }

            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        }


        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return GetHandbookDropsFromBreakDrops(handbookStack, forPlayer);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            List<ItemStack> todrop = new List<ItemStack>();

            for (int i = 0; i < Drops.Length; i++)
            {
                if (Drops[i].Tool != null && (byPlayer == null || Drops[i].Tool != byPlayer.InventoryManager.ActiveTool)) continue;

                ItemStack stack = Drops[i].GetNextItemStack(dropQuantityMultiplier);
                if (stack == null) continue;

                todrop.Add(stack);
                if (Drops[i].LastDrop) break;
            }

            return todrop.ToArray();
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }
    }
}
