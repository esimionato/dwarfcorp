using System.Runtime.Serialization;
using Newtonsoft.Json;
using System;
using Microsoft.Xna.Framework;

namespace DwarfCorp
{
    [Serializable] // Should never be serialized! But just in case, it works.
    public struct TemporaryVoxelHandle : IEquatable<TemporaryVoxelHandle>
    {
        public static TemporaryVoxelHandle InvalidHandle = new TemporaryVoxelHandle(new GlobalVoxelCoordinate(0, 0, 0));

        #region Cache

        [JsonIgnore]
        private VoxelChunk _cache_Chunk;

        [JsonIgnore]
        private int _cache_Index;

        private void UpdateCache(ChunkData Chunks)
        {
            _cache_Index = VoxelConstants.DataIndexOf(Coordinate.GetLocalVoxelCoordinate());
            _cache_Chunk = Chunks.ChunkMap.ContainsKey(Coordinate.GetGlobalChunkCoordinate()) ? Chunks.ChunkMap[Coordinate.GetGlobalChunkCoordinate()] : null;
        }

        [JsonIgnore]
        public VoxelChunk Chunk { get { return _cache_Chunk; } }

        #endregion

        public readonly GlobalVoxelCoordinate Coordinate;
        
        [JsonIgnore]
        public Vector3 WorldPosition { get { return Coordinate.ToVector3(); } }

        [JsonIgnore]
        public bool IsValid { get { return _cache_Chunk != null; } }

        public TemporaryVoxelHandle(ChunkData Chunks, GlobalVoxelCoordinate Coordinate)
        {
            this.Coordinate = Coordinate;
            this._cache_Chunk = null;
            this._cache_Index = 0;
            UpdateCache(Chunks);
        }

        [JsonConstructor]
        private TemporaryVoxelHandle(GlobalVoxelCoordinate Coordinate)
        {
            this.Coordinate = Coordinate;
            this._cache_Chunk = null;
            this._cache_Index = 0;
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            UpdateCache(((WorldManager)context.Context).ChunkManager.ChunkData);
        }

        public TemporaryVoxelHandle(VoxelChunk Chunk, LocalVoxelCoordinate Coordinate)
        {
            this.Coordinate = Chunk.ID + Coordinate;
            this._cache_Chunk = Chunk;
            this._cache_Index = VoxelConstants.DataIndexOf(Coordinate);
        }

        #region Equality
        public static bool operator ==(TemporaryVoxelHandle A, TemporaryVoxelHandle B)
        {
            return A.Coordinate == B.Coordinate;
        }

        public static bool operator !=(TemporaryVoxelHandle A, TemporaryVoxelHandle B)
        {
            return A.Coordinate != B.Coordinate;
        }

        public override int GetHashCode()
        {
            return Coordinate.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (!(obj is TemporaryVoxelHandle)) return false;
            return this == (TemporaryVoxelHandle)obj;
        }

        public bool Equals(TemporaryVoxelHandle other)
        {
            return this == other;
        }
        #endregion

        #region Voxel Properties

        [JsonIgnore]
        public RampType RampType
        {
            get { return _cache_Chunk.Data.RampTypes[_cache_Index]; }
            set { _cache_Chunk.Data.RampTypes[_cache_Index] = value; }
        }

        [JsonIgnore]
        public bool IsEmpty
        {
            get { return TypeID == 0; }
        }


        [JsonIgnore]
        public byte TypeID
        {
            get
            {
                return _cache_Chunk.Data.Types[_cache_Index];
            }
            set
            {
                OnTypeSet(VoxelType.TypeList[value]);
            }
        }

        // Todo: Eliminate members that aren't straight pass throughs to the underlying data.
        [JsonIgnore]
        public VoxelType Type
        {
            get
            {
                return VoxelType.TypeList[_cache_Chunk.Data.Types[_cache_Index]];
            }
            set
            {
                OnTypeSet(value);
            }
        }

        private void OnTypeSet(VoxelType NewType)
        {
            // Changing a voxel is actually a relatively rare event, so we can afford to do a bit of 
            // bookkeeping here.

            var previous = _cache_Chunk.Data.Types[_cache_Index];

            // Change actual data
            _cache_Chunk.Data.Types[_cache_Index] = (byte)NewType.ID;
            _cache_Chunk.Data.Health[_cache_Index] = (byte)NewType.StartingHealth;

            // Did we go from empty to filled or vice versa? Update filled counter.
            if (previous == 0 && NewType.ID != 0)
                _cache_Chunk.Data.VoxelsPresentInSlice[Coordinate.Y] += 1;
            else if (previous != 0 && NewType.ID == 0)
                _cache_Chunk.Data.VoxelsPresentInSlice[Coordinate.Y] -= 1;

            // Invalidate slice cache for current chunk.
            _cache_Chunk.Data.SliceCache[Coordinate.Y] = null;
            if (Coordinate.Y > 0) _cache_Chunk.Data.SliceCache[Coordinate.Y - 1] = null;

            // Invalidate slice cache for neighbor chunks.
            if (Coordinate.X == 0)
                InvalidateNeighborSlice(Chunk.Manager.ChunkData, Chunk.ID, new Point3(-1, 0, 0), Coordinate.Y);
            if (Coordinate.X == VoxelConstants.ChunkSizeX - 1)
                InvalidateNeighborSlice(Chunk.Manager.ChunkData, Chunk.ID, new Point3(1, 0, 0), Coordinate.Y);
            if (Coordinate.Z == 0)
                InvalidateNeighborSlice(Chunk.Manager.ChunkData, Chunk.ID, new Point3(0, 0, -1), Coordinate.Y);
            if (Coordinate.Z == VoxelConstants.ChunkSizeZ - 1)
                InvalidateNeighborSlice(Chunk.Manager.ChunkData, Chunk.ID, new Point3(0, 0, 1), Coordinate.Y);

            // Find the first filled voxel below and invalidate it's layer.
            if (Coordinate.Y > 0)
            {
                var localCoordinate = Coordinate.GetLocalVoxelCoordinate();
                var below = VoxelHelpers.FindFirstVoxelBelow(new TemporaryVoxelHandle(Chunk,
                    new LocalVoxelCoordinate(localCoordinate.X, localCoordinate.Y - 1, localCoordinate.Z)));
                if (below.IsValid)
                    _cache_Chunk.Data.SliceCache[below.Coordinate.Y] = null;
            }
        }

        private void InvalidateNeighborSlice(ChunkData Chunks, GlobalChunkCoordinate ChunkCoordinate,
            Point3 NeighborOffset, int Y)
        {
            var neighborCoordinate = new GlobalChunkCoordinate(ChunkCoordinate.X + NeighborOffset.X,
                ChunkCoordinate.Y + NeighborOffset.Y,
                ChunkCoordinate.Z + NeighborOffset.Z);

            VoxelChunk chunk = null;
            if (Chunks.ChunkMap.TryGetValue(neighborCoordinate, out chunk))
            {
                chunk.Data.SliceCache[Y] = null;
                if (Y > 0) chunk.Data.SliceCache[Y - 1] = null;
            }
        }

        [JsonIgnore]
        public bool IsVisible
        {
            get { return Coordinate.Y < _cache_Chunk.Manager.ChunkData.MaxViewingLevel; }
        }

        public BoundingBox GetBoundingBox()
        {
            var pos = Coordinate.ToVector3();
            return new BoundingBox(pos, pos + Vector3.One);
        }

        [JsonIgnore]
        public int SunColor
        {
            get { return _cache_Chunk.Data.SunColors[_cache_Index]; }
            set { _cache_Chunk.Data.SunColors[_cache_Index] = (byte)value; }
        }

        [JsonIgnore]
        public bool IsExplored
        {
            get { return !GameSettings.Default.FogofWar || _cache_Chunk.Data.IsExplored[_cache_Index]; }
            set { _cache_Chunk.Data.IsExplored[_cache_Index] = value; }
        }

        [JsonIgnore]
        public WaterCell WaterCell
        {
            get { return _cache_Chunk.Data.Water[_cache_Index]; }
            set {

                var existingLiquid = _cache_Chunk.Data.Water[_cache_Index];
                if (existingLiquid.Type != LiquidType.None && value.Type == LiquidType.None)
                    _cache_Chunk.Data.LiquidPresent[Coordinate.Y] -= 1;
                if (existingLiquid.Type == LiquidType.None && value.Type != LiquidType.None)
                    _cache_Chunk.Data.LiquidPresent[Coordinate.Y] += 1;

                _cache_Chunk.Data.Water[_cache_Index] = value;
            }
        }

        [JsonIgnore]
        public float Health
        {
            get { return (float)_cache_Chunk.Data.Health[_cache_Index]; }
            set
            {
                // Todo: Bad spot for this. Ideally is checked by whatever is trying to damage the voxel.
                if (Type.IsInvincible) return;
                _cache_Chunk.Data.Health[_cache_Index] = (byte)(Math.Max(Math.Min(value, 255.0f), 0.0f));
            }
        }

        #endregion

        public override string ToString()
        {
            return "voxel at " + Coordinate.ToString();
        }
    }
}
