﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Bigio.BigArray.Interfaces;
using Bigio.BigArray.Internal_Block_Collections;
using Bigio.BigArray.Managers;
using Bigio.Common.Managers;

namespace Bigio.BigArray.Support_Classes.BlockCollection
{
    /// <summary>
    /// BlockCollection is collection of blocks which abstracts you from internal work with block collection.
    /// </summary>
    /// <typeparam name="T">Type of block elements.</typeparam>
    public partial class BlockCollection<T> : ICollection<Block<T>>
    {
        //Data

        /// <summary>
        /// Collection with blocks provides the main data object in the Blocks class.
        /// </summary>
        private readonly IBigList<Block<T>> _blocks;

        /// <summary>
        /// Internal value of <see cref="DefaultBlockSize"/>.
        /// </summary>
        private int _defaultBlockSize;

        /// <summary>
        /// Internal value of <see cref="MaxBlockSize"/>.
        /// </summary>
        private int _maxBlockSize;

        //API

        /// <summary>
        /// Create a new instance of <see cref="BlockCollection{T}"/> class.
        /// <see cref="MaxBlockSize"/> and <see cref="DefaultBlockSize"/> initialize from default values of
        /// <see cref="DefaultValuesManager.MaxBlockSize"/> and <see cref="DefaultValuesManager.DefaultBlockSize"/> 
        /// </summary>
        public BlockCollection()
        {
            _blocks = new InternalBlockList<Block<T>>();

            MaxBlockSize = DefaultValuesManager.MaxBlockSize;
            DefaultBlockSize = DefaultValuesManager.DefaultBlockSize;
            IsReadOnly = false;
        }

        /// <summary>
        /// Create a new instance of <see cref="BlockCollection{T}"/> class and use <see cref="blockCollection"/> as block collection.
        /// </summary>
        /// <param name="blockCollection">Collection to set it as internal block collection for controll of it. It can't be null.</param>
        public BlockCollection(IBigList<Block<T>> blockCollection) : this(blockCollection, new Collection<T>())
        {
            
        }

        /// <summary>
        /// Create a new instance of <see cref="BlockCollection"/> and get blocks from specify <see cref="collection"/>
        /// </summary>
        /// <param name="collection">Collection whitch use as base for new <see cref="BigArray{T}"/>.
        /// The collection it self cannot be null and cant contain null blocks
        /// , if type T is a reference type.</param>
        public BlockCollection(ICollection<T> collection) : this()
        {
            _blocks = new InternalBlockList<Block<T>>();
            Initialize(collection);
        }

        /// <summary>
        /// Create a new instance of <see cref="Blocks{T}"/> class.
        /// </summary>
        /// <param name="blockCollection">Collection to set it as internal block collection
        /// for controll of it. It can't be null.</param>
        /// <param name="collection">Collection whitch use as base for new <see cref="BigArray{T}"/>.
        public BlockCollection(IBigList<Block<T>> blockCollection, ICollection<T> collection) : this()
        {
            if (blockCollection == null)
                throw new ArgumentOutOfRangeException("blockCollection");

            _blocks = blockCollection;
            Initialize(collection);
        }

        /// <summary>
        /// Add new block to the end of collection of blocks of the <see cref="BlockCollection{T}"/>.
        /// If your block is too big, function will try to divite it into blocks.
        /// If the block is empty, function won't add it(If you want to do it, you will must to use <see cref="AddNewBlock"/> function).
        /// If block is too big, it will be divide into several blocks.
        /// </summary>
        /// <param name="block">Block to add.</param>
        public void Add(Block<T> block)
        {
            if (block == null)
                throw new ArgumentNullException("block");

            Add(block, 0, block.Count);
        }

        /// <summary>
        /// Add new block to the end of collection of blocks of the <see cref="BlockCollection{T}"/>.
        /// If your block is too big, function will try to divite it into blocks.
        /// If the block is empty, function wont add it. 
        /// it(If you want to do it, you will must to use <see cref="AddNewBlock"/> function).
        /// If block is too big, it will be divided into several blocks.
        /// </summary>
        /// <param name="block">Block to add.</param>
        public void Add(ICollection<T> block)
        {
            if (block == null)
                throw new ArgumentNullException("block");

            Add(block, 0, block.Count);
        }

        /// <summary>
        /// Add data starting with index of block to the end of collection 
        /// of blocks of the <see cref="BlockCollection{T}"/> as new blocks.
        /// If your block is too big, function will try to divite it into blocks.
        /// If the block is empty, function wont add it. 
        /// it(If you want to do it, you will must to use <see cref="AddNewBlock"/> function).
        /// If block is too big, it will be divided into several blocks.
        /// </summary>
        /// <param name="block">Block to add.</param>
        /// <param name="blockSubindex">The zero-based index in block at which copying begins.</param>
        public void Add(ICollection<T> block, int blockSubindex)
        {
            Add(block, blockSubindex, block.Count - blockSubindex);
        }

        /// <summary>
        /// Add data from specified range of block to the end of collection
        /// of blocks of the <see cref="BlockCollection{T}"/> as new blocks.
        /// If your block is too big, function will try to divite it into blocks.
        /// If the block is empty, function wont add it. 
        /// it(If you want to do it, you will must to use <see cref="AddNewBlock"/> function).
        /// If block is too big, it will be divided into several blocks.
        /// </summary>
        /// <param name="block">Block to add.</param>
        /// <param name="blockSubindex">The zero-based index in block at which copying begins.</param>
        /// <param name="blockCount">The number of elements of block to copy.</param>
        public void Add(ICollection<T> block, int blockSubindex, int blockCount)
        {
            if (block.Count != 0)
                AddDividedBlockRange(DivideIntoBlocks(block, blockSubindex, blockCount));
        }

        /// <summary>
        /// Add new empty block as last block. New block will have <see cref="DefaultBlockSize"/> capacity.
        /// You need to use this function istead of Add(emptyBlock) because if
        /// block is empty Add() function don't add it in the BlockCollection. 
        /// If block is too big, it will be divide into several blocks.
        /// </summary>
        public void AddNewBlock()
        {
            _blocks.Add(new Block<T>(DefaultBlockSize));
        }

        /// <summary>
        /// Adds the blocks of the specified collection to the end of the <see cref="BlockCollection"/>.
        /// If block is too big, it will be divide into several blocks.
        /// All blocks in the range can't be null, but block can be empty
        /// (in this way function don't add block in the <see cref="BlockCollection"/>).
        /// </summary>
        /// <param name="range">The collection of blocks whose elements should be added 
        /// to the end of the <see cref="BlockCollection{T}"/>.
        /// The collection itself cannot benull and cant contain null elements.</param>
        public void AddRange(ICollection<Block<T>> range)
        {
            if (range == null)
                throw new ArgumentNullException("range");

            //Divide each block
            foreach (var block in range)
            {
                _blocks.AddRange(DivideIntoBlocks(block));
            }
        }

        /// <summary>
        /// Remove all blocks from the <see cref="BlockCollection{}"/>.
        /// </summary>
        public void Clear()
        {
            _blocks.Clear();
        }

        /// <summary>
        /// Remove true if <see cref="BlockCollection{}"/> contains value, otherwise return false.
        /// </summary>
        /// <param name="item">Block to be checked.</param>
        public bool Contains(Block<T> item)
        {
            return _blocks.Contains(item);
        }

        /// <summary>
        /// Copies the entire <see cref="BlockCollection{}"/> to a compatible one-dimensional array
        /// , starting at the specified <see cref="arrayIndex"/> of the target array.
        /// </summary>
        /// <param name="array">The one-dimensional Array that is the destination of the blocks copied from <see cref="BlockCollection{}"/>.
        ///  The <see cref="array"/> must have zero-based indexing. </param>
        /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
        public void CopyTo(Block<T>[] array, int arrayIndex)
        {
            if (array == null)
                throw new ArgumentNullException("array");

            if (!array.IsValidRange(arrayIndex, Count))
                throw new ArgumentOutOfRangeException("arrayIndex");

            _blocks.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection of blocks.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<Block<T>> GetEnumerator()
        {
            return new BlockCollectionEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Inserts an element into the <see cref="BigArray{T}"/> at the specified index.
        /// If your block is too big, function will try to divite it into blocks.
        /// If the block is empty, function will insert it. 
        /// it(If you want to do it, you will must to use InsertNewBlock function).
        /// </summary>
        /// <param name="index">Index of collection of block where the new block will be.</param>
        /// <param name="block">Block to be inserted.</param>
        public void Insert(int index, Block<T> block)
        {
            InsertRange(index, DivideIntoBlocks(block));
        }

        /// <summary>
        /// Insert new empty block at the specified idex. New block will have <see cref="DefaultBlockSize"/> capacity.
        /// You need to use this function istead of <see cref="Insert"/>(index, emptyBlock) because if
        /// block is empty, <see cref="Insert"/> function wont add it in the <see cref="BlockCollection"/>. 
        /// </summary>
        /// <param name="index"></param>
        public void InsertNewBlock(int index)
        {
            _blocks.Insert(index, new Block<T>(DefaultBlockSize));
        }

        /// <summary>
        /// Inserts the elements of a block range into the block collection at the specified <see cref="index"/>.
        /// If block is too big, it will be divide into several blocks.
        /// All blocks in the range cant be null, but block can be empty
        /// (in this way function dont insert block in the <see cref="BlockCollection"/>).
        /// </summary>
        /// <param name="index">The zero-based index at which the new elements should be inserted.</param>
        /// <param name="range">The range whose elements should be inserted into the <see cref="BlockCollection{T}"/>.
        ///  The range it self cannot be null, and cant contain null blocks.</param>
        public void InsertRange(int index, ICollection<Block<T>> range)
        {
            if (range == null)
                throw new ArgumentNullException("range");

            if (!this.IsValidIndex(index) && index != Count/*We also can insert item as last block*/)
                throw new ArgumentOutOfRangeException("index");

            var blocksToInsert = new List<Block<T>>(range.Count);

            //Divide each block
            foreach (var block in range)
            {
                blocksToInsert.AddRange(DivideIntoBlocks(block));
            }

            if (blocksToInsert.Count != 0)
                _blocks.InsertRange(index, blocksToInsert);
        }

        /// <summary>
        /// Removes the first occurrence of a specific block from the <see cref="BlockCollection{T}"/>.
        /// </summary>
        /// <param name="block">The object to remove from the <see cref="BigArray{T}"/>.</param>
        /// <returns>True if item is successfully removed; otherwise, false.
        ///  This method also returns false if item was not found in the <see cref="BlockCollection{T}"/>.</returns>
        public bool Remove(Block<T> block)
        {
            return _blocks.Remove(block);
        }

        /// <summary>
        /// Removes the block at the specified index of the <see cref="BlockCollection{T}"/>.
        /// </summary>
        /// <param name="index">The zero-based index of the block to remove.</param>
        public void RemoveAt(int index)
        {
            if (!this.IsValidIndex(index))
                throw new ArgumentOutOfRangeException("index");

            _blocks.RemoveAt(index);
        }

        /// <summary>
        /// Reverses the order of the blocks in the entire <see cref="BlockCollection{T}"/>.
        /// </summary>
        public void Reverse()
        {
           _blocks.Reverse();
        }

        /// <summary>
        /// If count of elements of block at specified index more or equal to <see cref="MaxBlockSize"/>,
        /// it will be divide into the new blocks with <see cref="DefaultBlockSize"/> size.
        /// </summary>
        /// <param name="index">Index of block to be divided.</param>
        public void TryToDivideBlock(int index)
        {
            if (!this.IsValidIndex(index))
                throw new ArgumentOutOfRangeException("index");

            int count = _blocks[index].Count;
            if (count >= MaxBlockSize)
            {
                var newBlocks = DivideIntoBlocks(_blocks[index]);
                RemoveAt(index);
                InsertRange(index, newBlocks);
            }
        }

        /// <summary>
        /// If there is no any blocks - add first empty block.
        /// </summary>
        public void AddFirstBlockIfThereIsNeeded()
        {
            if (Count == 0)
                AddNewBlock();
        }

        //Support functions

        /// <summary>
        /// Divide specified collection into blocks with <see cref="DefaultBlockSize"/> size.
        /// </summary>
        /// <param name="collection">Collection, which must be divided.</param>
        /// <returns>Blocks constructed on the basis of the collection with <see cref="DefaultBlockSize"/> size.</returns>
        private ICollection<Block<T>> DivideIntoBlocks(ICollection<T> collection)
        {
            if (collection == null)
                throw new ArgumentNullException("collection");

            if (collection.Count == 0)
                return new Block<T>[0];

            return DivideIntoBlocks(collection, 0, collection.Count);
        }

        /// <summary>
        /// Divide specified range of collection
        /// (starts at the specified <see cref="collectionIndex"/> and contaies specified count of elements)
        /// into blocks with <see cref="DefaultBlockSize"/> size.
        /// </summary>
        /// <param name="collection">Collection, which must be divided.</param>
        /// <param name="collectionIndex">The zero-based starting index of the <see cref="collection"/> of elements to divide.</param>
        /// <param name="countToDivide">The number of elements of the <see cref="collection"/> to divide.</param>
        /// <returns>Blocks constructed on the basis of the collection with <see cref="DefaultBlockSize"/> size.</returns>
        private IList<Block<T>> DivideIntoBlocks(ICollection<T> collection, int collectionIndex, int countToDivide)
        {
            if (collection == null)
                throw new ArgumentNullException("collection");

            if (!collection.IsValidRange(collectionIndex, countToDivide))
                throw new ArgumentOutOfRangeException();

            //Calculate blocks count
            int countOfBlocks = countToDivide / DefaultBlockSize;
            if (countToDivide % DefaultBlockSize != 0)
                countOfBlocks++;

            var blocks = new Block<T>[countOfBlocks];

            //Transfer data from collection to new blocks
            var item = collection.GetEnumerator();
            for (int i = 0; i < collectionIndex; i++) //Move item to the index position
            {
                item.MoveNext();
            }

            for (int i = 0; i < countOfBlocks; i++)
            {
                //Calculate curent block size
                int currentBlockSize;
                if (i != countOfBlocks - 1)
                    currentBlockSize = DefaultBlockSize;
                else
                    currentBlockSize = countToDivide - (i * DefaultBlockSize);

                //Declare new block
                blocks[i] = new Block<T>(DefaultBlockSize);

                //Transfer data
                for (int j = 0; j < currentBlockSize; j++)
                {
                    item.MoveNext();

                    T insertion = item.Current;
                    blocks[i].Add(insertion);
                }
            }

            return blocks;
        }

        /// <summary>
        /// Execute preliminary initialization of <see cref="BlockCollection{T}"/>'s internal data.
        /// </summary>
        /// <param name="collection">Collection to initialize <see cref="BlockCollection{T}"/> with it.</param>
        private void Initialize(ICollection<T> collection)
        {
            if (collection == null)
                throw new ArgumentNullException("collection");

            AddRange(DivideIntoBlocks(collection));
        }

        private void AddDividedBlockRange(ICollection<Block<T>> range)
        {
            if (range == null)
                throw new ArgumentNullException("range");

            _blocks.AddRange(range);
        }

        //Data

        /// <summary>
        /// Gets or sets the block at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the block to get or set.</param>
        public Block<T> this[int index]
        {
            get
            {
                return _blocks[index];
            }
        }

        /// <summary>
        /// Get the number of blocks actually contained in the block collection.
        /// </summary>
        public int Count
        {
            get
            {
                return _blocks.Count;
            }
        }

        /// <summary>
        /// Default size of one <see cref="BigArray{T}"/> block. 
        /// Because of the way memory allocation is most effective that it is a power of 2.
        /// </summary>
        public int DefaultBlockSize
        {
            get
            {
                return _defaultBlockSize;
            }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException("value");

                if (value > MaxBlockSize)
                    throw new ArgumentOutOfRangeException("value", "DefaultBlockSize cant be more than MaxBlockSize");

                _defaultBlockSize = value;
            }
        }

        public bool IsReadOnly { get; private set; }

        /// <summary>
        /// The size of any block never will be more than this number.
        /// Because of the way memory allocation is most effective that it is a power of 2.
        /// </summary>
        public int MaxBlockSize
        {
            get
            {
                return _maxBlockSize;
            }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException("value");
                }

                _maxBlockSize = value;
            }
        }
    }
}

