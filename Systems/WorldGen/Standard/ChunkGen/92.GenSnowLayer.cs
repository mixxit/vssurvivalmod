﻿using System;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{
    public class GenSnowLayer : ModStdWorldGen
    {
        ICoreServerAPI api;
        Random rnd;
        int worldheight;

        IBlockAccessor blockAccessor;
        BlockLayerConfig blockLayerConfig;

        int transSize;
        int maxTemp;
        int minTemp;


        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }


        public override double ExecuteOrder()
        {
            return 0;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;

            if (DoDecorationPass)
            {
                api.Event.InitWorldGenerator(initWorldGen, "standard");
                api.Event.ChunkColumnGeneration(OnChunkColumnGen, EnumWorldGenPass.NeighbourSunLightFlood, "standard");
                api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);
            }
        }

        private void initWorldGen()
        {
            LoadGlobalConfig(api);

            IAsset asset = api.Assets.Get("worldgen/blocklayers.json");
            blockLayerConfig = asset.ToObject<BlockLayerConfig>();

            blockLayerConfig.SnowLayer.BlockId = api.WorldManager.GetBlockId(blockLayerConfig.SnowLayer.BlockCode);

            rnd = new Random(api.WorldManager.Seed);
            chunksize = api.WorldManager.ChunkSize;
            worldheight = api.WorldManager.MapSizeY;

            transSize = blockLayerConfig.SnowLayer.TransitionSize;
            maxTemp = blockLayerConfig.SnowLayer.MaxTemp;
            minTemp = maxTemp - transSize;
        }

        private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
        {
            blockAccessor = chunkProvider.GetBlockAccessor(true);
        }



        private void OnChunkColumnGen(IServerChunk[] chunks, int chunkX, int chunkZ, ITreeAttribute chunkGenParams = null)
        {
            IntDataMap2D climateMap = chunks[0].MapChunk.MapRegion.ClimateMap;
            ushort[] heightMap = chunks[0].MapChunk.RainHeightMap;

            int regionChunkSize = api.WorldManager.RegionSize / chunksize;
            int rlX = chunkX % regionChunkSize;
            int rlZ = chunkZ % regionChunkSize;

            float facC = (float)climateMap.InnerSize / regionChunkSize;
            int climateUpLeft = climateMap.GetUnpaddedInt((int)(rlX * facC), (int)(rlZ * facC));
            int climateUpRight = climateMap.GetUnpaddedInt((int)(rlX * facC + facC), (int)(rlZ * facC));
            int climateBotLeft = climateMap.GetUnpaddedInt((int)(rlX * facC), (int)(rlZ * facC + facC));
            int climateBotRight = climateMap.GetUnpaddedInt((int)(rlX * facC + facC), (int)(rlZ * facC + facC));

            for (int x = 0; x < chunksize; x++)
            {
                for (int z = 0; z < chunksize; z++)
                {
                    int posY = heightMap[z * chunksize + x];

                    int climate = GameMath.BiLerpRgbColor((float)x / chunksize, (float)z / chunksize, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight);

                    float temp = TerraGenConfig.GetScaledAdjustedTemperatureFloat((climate >> 16) & 0xff, posY - TerraGenConfig.seaLevel);

                    int prevY = posY;
                    if (PlaceSnowLayer(x, prevY, z, chunks, temp))
                    {
                        heightMap[z * chunksize + x]++;
                    }
                }
            }
        }


        bool PlaceSnowLayer(int x, int posY, int z, IServerChunk[] chunks, float temp)
        {
            float transDistance = temp - minTemp;

            if (temp > maxTemp)
            {
                return false;
            }
            if (transDistance > rnd.NextDouble() * transSize)
            {
                return false;
            }

            while (posY < worldheight - 1 && chunks[(posY+1) / chunksize].Blocks[(chunksize * ((posY + 1) % chunksize) + z) * chunksize + x] != 0)
            {
                posY++;
            }

            if (posY >= worldheight - 1) return false;

            int blockId = chunks[posY / chunksize].Blocks[(chunksize * (posY % chunksize) + z) * chunksize + x];
            Block block = api.World.Blocks[blockId];
            if (block.SnowCoverage == null && block.SideSolid[BlockFacing.UP.Index] || (block.SnowCoverage == true))
            {
                chunks[(posY + 1) / chunksize].Blocks[(chunksize * ((posY + 1) % chunksize) + z) * chunksize + x] = blockLayerConfig.SnowLayer.BlockId;
                return true;
            }

            return false;
        }



    }
}
