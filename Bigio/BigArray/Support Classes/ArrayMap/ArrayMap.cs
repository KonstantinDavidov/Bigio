﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Bigio.BigArray.Interfaces;
using Bigio.BigArray.Support_Classes.BlockCollection;
using Bigio.Common.Classes;
using Bigio.Common.Managers;

namespace Bigio.BigArray.Support_Classes.ArrayMap
{
    /// <summary>
    /// This class used for get information about <see cref="BlockCollection{T}"/> structure,
    /// for example for searching block with specified index.
    /// </summary>
    internal class ArrayMap<T>
    {
        //Data

        /// <summary>
        /// We use this flag to show that all <see cref="BlockInfo(int)"/> in <see cref="_blocksInfoList"/> is up to date.
        /// </summary>
        private const int NoBlockChanges = -1;

        /// <summary>
        /// We use this flag to show that <see cref="_cachedCountInfo"/> we use in <see cref="GetCachedElementCount"/> has invalid value
        /// and need to be updated.
        /// </summary>
        private const int CountCacheInvalid = -2;

		/// <summary>
		/// It's a cached data about current count of cached element. We need it to get this information faster.
		/// (it's very usefull in time of MultyblockRange and ReverseMultyblockRange).
		/// </summary>
		private CachedCountInfo _cachedCountInfo = new CachedCountInfo(CountCacheInvalid, CountCacheInvalid);

        /// <summary>
        /// Parent <see cref="BlockCollection"/> to define structure of it.
        /// </summary>
        private BlockCollection<T> _blockCollection;

        /// <summary>
        /// Information about each block, which contain in <see cref="_blockCollection"/>.
        /// It can be defferent from real information if data changed and user don't update information.
        /// Important: _blocksInfoList can be obsoloete if <see cref="_indexOfFirstChangedBlock"/> was changed!
        /// </summary>
        private readonly List<BlockInfo> _blocksInfoList;

        /// <summary>
        /// It is an index of first changed block. Next to this block information of <see cref="_blocksInfoList"/> is obsolete.
        /// If block structure is up to date _indexOfFirstChangedBlock equals to <see cref="NoBlockChanges"/>
        /// </summary>
        private int _indexOfFirstChangedBlock;

        /// <summary>
        /// Balancer for determination of blocks size
        /// </summary>
        private readonly IBalancer _balancer;

	    private readonly object _locker = new object();

        //API

        /// <summary>
        /// Create new instance of <see cref="ArrayMap{T}"/> describing specify <see cref="BlockCollection"/>
        /// </summary>
        /// <param name="balancer"></param>
        /// <param name="blockCollection"><see cref="BlockCollection"/> to descibe.</param>
        public ArrayMap(IBalancer balancer, BlockCollection<T> blockCollection)
        {
            _balancer = balancer;
            BlockCollection = blockCollection;

            _blocksInfoList = new List<BlockInfo>(blockCollection.Count);
            if (BlockCollection.Count != 0)
            {
                DataChanged(0);
            }
        }

        /// <summary>
        /// Calculate information about block and location of block inside <see cref="BlockCollection{T}"/>.
        /// </summary>
        /// <param name="index">Common zero-based index of element in <see cref="BigArray{T}"/>.
        ///  It's to find parent block.</param>
        public BlockInfo BlockInfo(int index)
        {
            return BlockInfo(index, 0);
        }

        /// <summary>
        /// Calculate information about block and location of block inside <see cref="BlockCollection{T}"/>.
        /// </summary>
        /// <param name="index">Common zero-based index of element in <see cref="BigArray{T}"/>. 
        /// It's to find parent block.</param>
        /// <param name="statBlockIndex">Function will try to find block, which containes
        /// specified index from statBlockIndex to last block of <see cref="BlockCollection{T}"/>.
        /// It use to get better performance.</param>
        public BlockInfo BlockInfo(int index, int statBlockIndex)
        {
            return BlockInfo(index, new Range(statBlockIndex, _blockCollection.Count - statBlockIndex));
        }

        /// <summary>
        /// Calculate information about block and location of block inside <see cref="BlockCollection{T}"/>.
        /// </summary>
        /// <param name="index">Common zero-based index of element in <see cref="BigArray{T}"/>.
        ///  It's to find parent block.</param>
        /// <param name="searchBlockRange">Function will try to find block, which containes
        /// specified index in this range of <see cref="BlockCollection{T}"/>.
        /// It use to get better performance.</param>
        public BlockInfo BlockInfo(int index, Range searchBlockRange)
        {
	        lock (_locker)
	        {
				if (!ValidationManager.IsValidRange(_blockCollection.Count, searchBlockRange))
					throw new ArgumentOutOfRangeException("searchBlockRange");

				if (index < GetCachedElementCount())
					return BinaryBlockInfo(index, searchBlockRange);

				return LinearBlockInfo(index, searchBlockRange);
	        }
        }

        /// <summary>
        /// Calculate a block range for all blocks that overlap with specified range.
        /// Block range provide information about overlapping specified <param name="calcRange"></param> and block.
        /// </summary>
        /// <param name="calcRange">Range to get multyblock range.</param>
        /// <returns>Return MultyblockRange object provides information about overlapping of specified <see cref="calcRange"/> and block.</returns>
        public MultyblockRange MultyblockRange(Range calcRange)
        {
	        lock (_locker)
	        {
				//If we want to select empty block
				// It's might be a special case, because sometimes we have to select empty range of
				// empty collection (0 blocks).
				if (calcRange.Count == 0)
				{
					return (calcRange.Index == 0)
						? new MultyblockRange(0, 0, new BlockRange[0])
						: new MultyblockRange
							(BlockInfo(calcRange.Index).CommonStartIndex, 0, new BlockRange[0]);
				}

				int indexOfStartBlock = BlockInfo(calcRange.Index).IndexOfBlock;
				int indexOfEndBlock = BlockInfo(calcRange.Index + calcRange.Count - 1, indexOfStartBlock).IndexOfBlock;
				int countOfBlocks = indexOfEndBlock - indexOfStartBlock + 1;

				return new MultyblockRange(indexOfStartBlock, countOfBlocks
					, CalculateMultyblockRanges(calcRange, indexOfStartBlock, indexOfEndBlock));
	        }
        }

        /// <summary>
        /// Calculate a reverse block range for all blocks that overlap with specified <see cref="calcRange"/>.
        /// Block range provide information about overlapping specified range and block.
        /// ReverseMultyblockRange start with last BlockRange(IndexOfStartBlock is index of last overlap block)
        /// so first element in ReverseMultyblockRange is information about last overlapping block.
        /// Every blockInformation object also contain REVERSE data(for example: blockInfo object of
        /// full overlapping of block with 100 element has Subindex as 99(index of last overlapping element).
        /// </summary>
        /// <param name="calcRange">REVERSE range to get multyblock range.
        /// For example: if you want to get ReverseMultyblockRange of array with one block with 100 element,
        /// you will must write this: structureObject.ReverseMultyblockRange(99, 100);
        /// </param>
        /// <returns>Return reverse <see cref="ArrayMap.MultyblockRange"/> object provides information about reverse overlapping of specified 
        /// <see cref="calcRange"/> and block.</returns>
        public MultyblockRange ReverseMultyblockRange(Range calcRange)
        {
			//TODO: rewrite method and make truly reverse multyblock range calculation
			//TODO: to make lazy calculations work. 
	        lock (_locker)
	        {
				if (calcRange.Index < 0 || calcRange.Count < 0) //Other checks are in the MultyblockRange() 
					throw new ArgumentOutOfRangeException();

				int directIndex = (calcRange.Index == 0 && calcRange.Count == 0)
					? 0 : calcRange.Index - calcRange.Count + 1;
				var range = MultyblockRange(new Range(directIndex, calcRange.Count));

				int indexOfStartBlock = range.IndexOfStartBlock + range.Count - 1;
				if (indexOfStartBlock < 0)
					indexOfStartBlock = 0;

				//Reverse all blocks in range
				var reverseBlockRanges = new BlockRange[range.Count];
				int counter = 0;
				foreach (var blockRange in range.Ranges)
				{
					var reverseBlockRange = new BlockRange(blockRange.Subindex + blockRange.Count - 1
						, blockRange.Count, blockRange.CommonStartIndex);

					reverseBlockRanges[range.Count - counter - 1] = reverseBlockRange;
					counter++;
				}

				return new MultyblockRange(indexOfStartBlock, range.Count, reverseBlockRanges);
	        }
        }

        /// <summary>
        /// Parent collection of blocks, which structure is ArrayMap.
        /// </summary>
        public BlockCollection<T> BlockCollection
        {
            private set
            {
	            lock (_locker)
	            {
					if (value == null)
						throw new ArgumentNullException("value");

					_blockCollection = value;
	            }
            }
            get
            {
	            lock (_locker)
	            {
					return _blockCollection;
	            }
            }
        }

        /// <summary>
        /// You need to call this function after you changed data in BigArray
        /// to update structure of ArrayMap when it will be need to do.
        /// </summary>
        public void DataChanged(int blockIndex)
        {
	        lock (_locker)
	        {
				Debug.Assert(blockIndex < _blockCollection.Count);
				Debug.Assert(blockIndex >= 0);

				_indexOfFirstChangedBlock = _indexOfFirstChangedBlock == NoBlockChanges ?
					blockIndex : Math.Min(_indexOfFirstChangedBlock, blockIndex);
				_cachedCountInfo.CachedIndexOfFirstChangedBlock = CountCacheInvalid;
	        }
        }

        /// <summary>
        /// You need to call this function after you changed data in BigArray
        /// to update structure of ArrayMap when it will be need to do.
        /// This version of <see cref="DataChanged"/> used in cases of any block removed, because
        /// it is possible that it was the last block and _blocksInfoList up to date.
        /// </summary>
        public void DataChangedAfterBlockRemoving(int blockIndex)
        {
	        lock (_locker)
	        {
				//If we removed last block(and block with blockIndex did't exists) or several last blocks
				if (blockIndex >= _blockCollection.Count)
				{
					//If there was only changes in last block(which already didn't exsist) then we up to date now
					//Note: also we need to do it, because otherwise _indexOfFirstChangedBlock will be contains index of
					//non-existent block.
					if (_indexOfFirstChangedBlock >= blockIndex)
						_indexOfFirstChangedBlock = NoBlockChanges;

					_cachedCountInfo.CachedIndexOfFirstChangedBlock = CountCacheInvalid;

					return;
				}

				DataChanged(blockIndex);
	        }
        }

        /// <summary>
        /// Performs lazy calculations to get blocks of MultyblockRange.
        /// </summary>
        /// <param name="calcRange">Range to get multyblock range.</param>
        /// <param name="indexOfStartBlock">Index of start block of range, which contain calcRange.</param>
        /// <param name="indexOfEndBlock">Index of end block of range, which contain calcRange.</param>
        /// <returns></returns>
        private IEnumerable<BlockRange> CalculateMultyblockRanges
            (Range calcRange, int indexOfStartBlock, int indexOfEndBlock)
        {
	        lock (_locker)
	        {
				var infoOfStartBlock = _blocksInfoList[indexOfStartBlock];
				var currentStartIndex = infoOfStartBlock.CommonStartIndex;
				var currentEndIndex = infoOfStartBlock.CommonStartIndex;
				var endIndex = calcRange.Index + calcRange.Count - 1;

				for (int i = indexOfStartBlock; i <= indexOfEndBlock; i++)
				{
					var block = _blockCollection[i];
					currentEndIndex += block.Count;

					//Current block can contain left border, right border or 
					// completely contain in specified range
					bool isRangeStartInThisBlock = calcRange.Index >= currentStartIndex;
					bool isRangeEndInThisBlock = endIndex >= currentEndIndex;

					int startSubindex = (isRangeStartInThisBlock) ? calcRange.Index - currentStartIndex : 0;
					int rangeCount = (isRangeEndInThisBlock) ? block.Count - startSubindex
						: endIndex - currentStartIndex - startSubindex + 1;

					if (rangeCount >= 0)
						yield return new BlockRange(startSubindex, rangeCount, currentStartIndex);

					currentStartIndex += block.Count;
				}
	        }
        }

        /// <summary>
        /// It is way to find block we already cached. This method have log(2,n) complexity and use some kind of
        /// binary search.
        /// </summary>
        /// <param name="index">Index to find information about block, which containes it.</param>
        /// <param name="searchBlockRange">Block range to find index in it. It use to get better performance.</param>
        /// <returns>Information about block, which contain specified index.</returns>
        private BlockInfo BinaryBlockInfo(int index, Range searchBlockRange)
        {
	        lock (_locker)
	        {
				if (!ValidationManager.IsValidIndex(GetCachedElementCount(), index))
					throw new ArgumentOutOfRangeException("index");

				if (searchBlockRange.Count == 0)
				{
					Debug.Assert(_blocksInfoList.IsValidIndex(searchBlockRange.Index));
					return new BlockInfo();
				}

				//We use some kind if binary search to find block with specified idex

				int indexOfStartBlock = searchBlockRange.Index;

				int indexOfLastCachedBlock = GetCachedBlockCount() - 1;
				if (indexOfLastCachedBlock == -1)
					indexOfLastCachedBlock = 0;
				int indexOfEndBlock = Math.Min(indexOfStartBlock + searchBlockRange.Count - 1, indexOfLastCachedBlock);

				// In code below we check is index in range. We don't do it before cycle 
				// to get better performance(suggestingPosition often is right and in this way we get
				// very bad performance)
				bool isIndexInCheckRange = false;

				while (indexOfStartBlock <= indexOfEndBlock)
				{
					//Calc middle position
					var startBlockInfo = _blocksInfoList[indexOfStartBlock];
					var endBlockInfo = _blocksInfoList[indexOfEndBlock];

					double startIndex = startBlockInfo.CommonStartIndex;
					double endIndex = endBlockInfo.CommonStartIndex + endBlockInfo.Count - 1;
					double countOfBlocks = endBlockInfo.IndexOfBlock - startBlockInfo.IndexOfBlock + 1;

					//We do it to check it only once 
					if (!isIndexInCheckRange)
					{
						if (index < startIndex || index > endIndex)
							throw new ArgumentOutOfRangeException("searchBlockRange");

						isIndexInCheckRange = true;
					}

					double suggestingBlockPosition;
					if (index == startBlockInfo.CommonStartIndex)
						suggestingBlockPosition = startBlockInfo.IndexOfBlock;
					else
					{
						suggestingBlockPosition = indexOfStartBlock
							+ (index - startIndex) * countOfBlocks / (endIndex - startIndex + 1);
					}

					//Compare
					var newBlockPosition = (int)suggestingBlockPosition;
					var blockInfo = _blocksInfoList[newBlockPosition];
					int result = blockInfo.Compare(index);
					switch (result)
					{
						case -1:
							indexOfEndBlock = newBlockPosition - 1;
							break;
						case 0:
							return blockInfo;
						case 1:
							indexOfStartBlock = newBlockPosition + 1;
							break;
					}
				}

				//We should not reach this string!
				throw new InvalidOperationException("There is no such index in specified range!");
	        }
        }

        /// <summary>
        /// It is way to find needed block if we don't cache it yet. This method have O(n) complexity.
        /// In fact we just get last cached block and move next until we find block we need. Also we cache all new blocks until needed.
        /// </summary>
        /// <param name="index">Index to find information about block, which containes it.</param>
        /// <param name="searchBlockRange"><see cref="Range"/> to find index in it. It use to get better performance.</param>
        /// <returns>Information about block, which contain specified index.</returns>
        private BlockInfo LinearBlockInfo(int index, Range searchBlockRange)
        {
	        lock (_locker)
	        {
				Debug.Assert(index >= GetCachedElementCount());

				if (!_blockCollection.IsValidRange(searchBlockRange))
					throw new ArgumentOutOfRangeException("searchBlockRange");

				//Move to start
				var startBlock = GetStartBlockInfoForLinear();
				if (startBlock.Compare(index) == 0)
					return startBlock;

				int commonStartIndex = startBlock.CommonStartIndex + startBlock.Count;

				Debug.Assert(startBlock.CommonStartIndex + startBlock.Count <= index);

				//Start block must be last cached block info
				Debug.Assert(startBlock.IndexOfBlock + 1 == _blocksInfoList.Count);

				//Add new blocks while we try to find needed block
				for (int i = startBlock.IndexOfBlock + 1; i < searchBlockRange.Index + searchBlockRange.Count; i++)
				{
					var elementCount = _blockCollection[i].Count;
					var newBlock = new BlockInfo(i, commonStartIndex, elementCount);
					_blocksInfoList.Add(newBlock);

					//If there is needed block
					if (index >= commonStartIndex && index < commonStartIndex + elementCount)
					{
						if (i + 1 == _blockCollection.Count)
							_indexOfFirstChangedBlock = NoBlockChanges;
						else
							_indexOfFirstChangedBlock = i + 1;

						return newBlock;
					}

					commonStartIndex += elementCount;
				}

				throw new ArgumentOutOfRangeException("index");
	        }
        }

        /// <summary>
        /// Get first cached <see cref="ArrayMap.BlockInfo"/> to start with in <see cref="LinearBlockInfo"/>.
        /// If we don't have anyone - add first <see cref="ArrayMap.BlockInfo"/>
        /// </summary>
        private BlockInfo GetStartBlockInfoForLinear()
        {
	        lock (_locker)
	        {
				var blockInfoListCount = _blocksInfoList.Count;

				Debug.Assert(_blockCollection.Count != 0); //it's must handled in LinearSearch

				//Remove obsolete blockInfos
				if (_indexOfFirstChangedBlock < blockInfoListCount)
					_blocksInfoList.RemoveRange(_indexOfFirstChangedBlock, blockInfoListCount - _indexOfFirstChangedBlock);

				Debug.Assert(_indexOfFirstChangedBlock == _blocksInfoList.Count);

				//Add first block if we have to
				if (_indexOfFirstChangedBlock == 0)
				{
					Debug.Assert(_blocksInfoList.Count == 0);

					_blocksInfoList.Add(new BlockInfo(0, 0, _blockCollection[0].Count));
					_indexOfFirstChangedBlock = _blockCollection.Count == 1 ? NoBlockChanges : 1;

					return _blocksInfoList[0];
				}

				return _blocksInfoList[_indexOfFirstChangedBlock - 1];
	        }
        }

        /// <summary>
        /// Get count of elements we already have in our cache. It might be helpful to determine what kind of search 
        /// need to call.
        /// </summary>
        /// <returns>Count of cached elements.</returns>
        private int GetCachedElementCount()
        {
	        lock (_locker)
	        {
				//Maybe we already know answer
				if (_cachedCountInfo.CachedIndexOfFirstChangedBlock == _indexOfFirstChangedBlock)
					return _cachedCountInfo.CachedCount;

				if (_indexOfFirstChangedBlock == NoBlockChanges)
				{
					if (BlockCollection.Count == 0)
					{
						_cachedCountInfo.CachedIndexOfFirstChangedBlock = NoBlockChanges;
						_cachedCountInfo.CachedCount = 0;

						return 0;
					}

					//BlockCollection and _blocksInfoList must be equal at this point
					Debug.Assert(BlockCollection.Count == _blocksInfoList.Count);
					var cachedCountOfLastBlock = GetCountByEndBlock(_blocksInfoList[_blocksInfoList.Count - 1]);

					_cachedCountInfo.CachedIndexOfFirstChangedBlock = NoBlockChanges;
					_cachedCountInfo.CachedCount = cachedCountOfLastBlock;

					return cachedCountOfLastBlock;
				}

				if (_indexOfFirstChangedBlock == 0)
				{
					_cachedCountInfo.CachedIndexOfFirstChangedBlock = 0;
					_cachedCountInfo.CachedCount = 0;

					return 0;
				}

				var cachedCount = GetCountByEndBlock(_blocksInfoList[_indexOfFirstChangedBlock - 1]);

				_cachedCountInfo.CachedIndexOfFirstChangedBlock = _indexOfFirstChangedBlock;
				_cachedCountInfo.CachedCount = cachedCount;

				return cachedCount;
	        }
        }

        /// <summary>
        /// Get real count of cached blocks we have.
        /// </summary>
        /// <returns>Real count of cached blocks we have</returns>
        private int GetCachedBlockCount()
        {
	        lock (_locker)
	        {
				if (_indexOfFirstChangedBlock == NoBlockChanges)
					return _blocksInfoList.Count;

				return _indexOfFirstChangedBlock;
	        }
        }

        /// <summary>
        /// Get count of elements in range of start block to specife <param name="endBlockInfo"></param>
        /// </summary>
        /// <param name="endBlockInfo">End block of block range to get element count of</param>
        /// <returns>Count of elements in range of start block to specify <param name="endBlockInfo"></param></returns>
        private int GetCountByEndBlock(BlockInfo endBlockInfo)
        {
	        lock (_locker)
	        {
				return endBlockInfo.CommonStartIndex + endBlockInfo.Count;
	        }
        }
    }
}
