using System.Collections.Generic;
using System.Linq;
using Mewdeko.Core.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Core.Services.Database.Repositories.Impl
{
    public abstract class Repository<T> : IRepository<T> where T : DbEntity
    {
        public Repository(DbContext context)
        {
            _context = context;
            _set = context.Set<T>();
        }

        protected DbContext _context { get; set; }
        protected DbSet<T> _set { get; set; }

        public void Add(T obj)
        {
            _set.Add(obj);
        }

        public void AddRange(params T[] objs)
        {
            _set.AddRange(objs);
        }

        public T GetById(int id)
        {
            return _set.FirstOrDefault(e => e.Id == id);
        }

        public IEnumerable<T> GetAll()
        {
            return _set.ToList();
        }

        public void Remove(int id)
        {
            _set.Remove(GetById(id));
        }

        public void Remove(T obj)
        {
            _set.Remove(obj);
        }

        public void RemoveRange(params T[] objs)
        {
            _set.RemoveRange(objs);
        }

        public void Update(T obj)
        {
            _set.Update(obj);
        }

        public void UpdateRange(params T[] objs)
        {
            _set.UpdateRange(objs);
        }
    }
}