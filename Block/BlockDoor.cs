﻿using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockDoor : BlockBaseDoor
    {
        bool airtight;

        public override void OnLoaded(ICoreAPI api)
        {
            airtight = Variant["material"] != "log";
            base.OnLoaded(api);
        }

        public override string GetKnobOrientation()
        {
            return GetKnobOrientation(this);
        }

        public override BlockFacing GetDirection()
        {
            return BlockFacing.FromCode(Variant["horizontalorientation"]);
        }

        public string GetKnobOrientation(Block block)
        {
            return Variant["knobOrientation"];
        }

        public bool IsUpperHalf()
        {
            return Variant["part"] == "up";
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            BlockPos abovePos = blockSel.Position.AddCopy(0, 1, 0);
            IBlockAccessor ba = world.BlockAccessor;

            if (ba.GetBlockId(abovePos) == 0 && CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                BlockFacing[] horVer = SuggestedHVOrientation(byPlayer, blockSel);

                string knobOrientation = GetSuggestedKnobOrientation(ba, blockSel.Position, horVer[0]);

                AssetLocation downBlockCode = CodeWithVariants(new Dictionary<string, string>() { 
                    { "horizontalorientation", horVer[0].Code },
                    { "part", "down" },
                    { "state", "closed" },
                    { "knobOrientation", knobOrientation }
                });

                Block downBlock = ba.GetBlock(downBlockCode);

                AssetLocation upBlockCode = downBlock.CodeWithVariant("part", "up");
                Block upBlock = ba.GetBlock(upBlockCode);

                ba.SetBlock(downBlock.BlockId, blockSel.Position);
                ba.SetBlock(upBlock.BlockId, abovePos);
                return true;
            }

            return false;
        }

        private string GetSuggestedKnobOrientation(IBlockAccessor ba, BlockPos pos, BlockFacing facing)
        {
            string leftOrRight = "left";

            Block nBlock1 = ba.GetBlock(pos.AddCopy(facing.GetCW()));
            Block nBlock2 = ba.GetBlock(pos.AddCopy(facing.GetCCW()));

            bool isDoor1 = IsSameDoor(nBlock1);
            bool isDoor2 = IsSameDoor(nBlock2);
            if (isDoor1 && isDoor2)
            {
                leftOrRight = "left";
            }
            else
            {
                if (isDoor1)
                {
                    leftOrRight = GetKnobOrientation(nBlock1) == "right" ? "left" : "right";
                }
                else if (isDoor2)
                {
                    leftOrRight = GetKnobOrientation(nBlock2) == "right" ? "left" : "right";
                }
            }
            return leftOrRight;
        }

        public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
        {
            BlockPos otherPos = pos.AddCopy(0, IsUpperHalf() ? -1 : 1, 0);
            Block otherPart = world.BlockAccessor.GetBlock(otherPos);

            if (otherPart is BlockDoor && ((BlockDoor)otherPart).IsUpperHalf() != IsUpperHalf())
            {
                world.BlockAccessor.SetBlock(0, otherPos);
            }

            Block block = world.BlockAccessor.GetBlock(pos);
            if (block is BlockDoor)
            {
                world.BlockAccessor.SetBlock(0, pos);
            }
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            return new ItemStack[] { OnPickBlock(world, pos) };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            AssetLocation blockCode = CodeWithVariants(new Dictionary<string, string>() {
                    { "horizontalorientation", "north" },
                    { "part", "down" },
                    { "state", "closed" },
                    { "knobOrientation", "left" }
                });

            Block block = world.BlockAccessor.GetBlock(blockCode);

            return new ItemStack(block);
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            base.OnNeighbourBlockChange(world, pos, neibpos);
        }

        protected override void Open(IWorldAccessor world, IPlayer byPlayer, BlockPos position)
        {
            float breakChance = Attributes["breakOnTriggerChance"].AsFloat(0);
            if (world.Side == EnumAppSide.Server && world.Rand.NextDouble() < breakChance && byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                world.BlockAccessor.BreakBlock(position, byPlayer);
                world.PlaySoundAt(new AssetLocation("sounds/effect/toolbreak"), position.X + 0.5, position.Y + 0.5, position.Z + 0.5, null);

                return;
            }

            AssetLocation newCode = CodeWithVariant("state", IsOpened() ? "closed" : "opened");
            Block newBlock = world.BlockAccessor.GetBlock(newCode);

            AssetLocation otherNewCode = newBlock.CodeWithVariant("part", IsUpperHalf() ? "down" : "up");

            world.BlockAccessor.ExchangeBlock(newBlock.BlockId, position);
            world.BlockAccessor.MarkBlockDirty(position);

            BlockPos otherPos = position.AddCopy(0, IsUpperHalf() ? -1 : 1, 0);
            Block otherPart = world.BlockAccessor.GetBlock(otherPos);


            if (otherPart is BlockDoor && ((BlockDoor)otherPart).IsUpperHalf() != IsUpperHalf())
            {
                world.BlockAccessor.ExchangeBlock(world.BlockAccessor.GetBlock(otherNewCode).BlockId, otherPos);
                world.BlockAccessor.MarkBlockDirty(otherPos);
            }
        }

        protected override BlockPos TryGetConnectedDoorPos(BlockPos pos)
        {
            string knob = GetKnobOrientation();
            BlockFacing dir = GetDirection();
            return knob == "left" ? pos.AddCopy(dir.GetCCW()) : pos.AddCopy(dir.GetCW());
        }

        public override AssetLocation GetRotatedBlockCode(int angle)
        {
            BlockFacing beforeFacing = BlockFacing.FromCode(LastCodePart(3));
            int rotatedIndex = GameMath.Mod(beforeFacing.HorizontalAngleIndex - angle / 90, 4);
            BlockFacing nowFacing = BlockFacing.HORIZONTALS_ANGLEORDER[rotatedIndex];

            return CodeWithVariant("horizontalorientation", nowFacing.Code);
        }

        public override int GetHeatRetention(BlockPos pos, BlockFacing facing)
        {
            if (!airtight) return 0;
            return open ? 3 : 1;
        }
    }
}

