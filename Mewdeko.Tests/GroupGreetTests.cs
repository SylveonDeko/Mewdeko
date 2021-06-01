using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Mewdeko.Core.Services;
using NUnit.Framework;

namespace Mewdeko.Tests
{
    public class GroupGreetTests
    {
        private GreetGrouper<int> _grouper;

        [SetUp]
        public void Setup()
        {
            _grouper = new GreetGrouper<int>();
        }

        [Test]
        public void CreateTest()
        {
            var created = _grouper.CreateOrAdd(0, 5);
            
            Assert.True(created);
        }
        
        [Test]
        public void CreateClearTest()
        {
            _grouper.CreateOrAdd(0, 5);
            _grouper.ClearGroup(0, 5, out var items);

            Assert.AreEqual(0, items.Count());
        }
        
        [Test]
        public void NotCreatedTest()
        {
            _grouper.CreateOrAdd(0, 5);
            var created = _grouper.CreateOrAdd(0, 4);
            
            Assert.False(created);
        }
        
        [Test]
        public void ClearAddedTest()
        {
            _grouper.CreateOrAdd(0, 5);
            _grouper.CreateOrAdd(0, 4);
            _grouper.ClearGroup(0, 5, out var items);

            var list = items.ToList();
            
            Assert.AreEqual(1, list.Count, $"Count was {list.Count}");
            Assert.AreEqual(4, list[0]);
        }

        [Test]
        public async Task ClearManyTest()
        {
            _grouper.CreateOrAdd(0, 5);
            
            // add 15 items
            await Task.WhenAll(Enumerable.Range(10, 15)
                .Select(x => Task.Run(() => _grouper.CreateOrAdd(0, x))));
            
            // get 5 at most
            _grouper.ClearGroup(0, 5, out var items);
            var list = items.ToList();
            Assert.AreEqual(5, list.Count, $"Count was {list.Count}");

            // try to get 15, but there should be 10 left
            _grouper.ClearGroup(0, 15, out items);
            list = items.ToList();
            Assert.AreEqual(10, list.Count, $"Count was {list.Count}");
        }
    }
}