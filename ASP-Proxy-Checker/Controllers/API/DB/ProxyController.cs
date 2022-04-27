using Microsoft.AspNetCore.Mvc;
using ProxyChecker.Database;
using ProxyChecker.Database.Models;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace ProxyChecker.Controllers.API.DB
{
    /// <summary>
    /// REST API для управления прокси для сканирования
    /// </summary>
    [Route("api/db/[controller]")]
    [ApiController]
    public class ProxyController : ControllerBase
    {
        public static bool ProxyIsBlocked(DatabaseContext db, Proxy proxy)
        {
            if (db.BlockedProxies.Any(x => x.Ip == proxy.RealAddress))
                return true;

            if (db.BlockedProxies.Any(x => proxy.RealAddress.StartsWith(x.Mask)))
                return true;

            return false;
        }

        public static bool ProxyIsBlocked(Proxy proxy)
        {
            using (var db = new DatabaseContext())
                return ProxyIsBlocked(db, proxy);
        }

        // GET: api/<ProxyController>
        [HttpGet]
        public IEnumerable<Proxy> Get()
        {
            try
            {
                var resultProxies = new List<Proxy>();

                using (var db = new DatabaseContext())
                {
                    foreach (Proxy proxy in db.Proxies)
                    {
                        if (ProxyIsBlocked(db, proxy))
                            continue;
    
                        resultProxies.Add(proxy);
                    }
                }

                return resultProxies;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return null;
        }

        // GET api/<ProxyController>/5
        [HttpGet("{id}")]
        public Proxy Get(int id)
        {
            try
            {
                using (var db = new DatabaseContext())
                {
                    var entry = db.Proxies.FirstOrDefault(x => x.Id == id);
                    if (entry != null && !ProxyIsBlocked(db, entry))
                        return entry;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return null;
        }

        // POST api/<ProxyController>
        [HttpPost]
        public void Post([FromBody] Proxy value)
        {
            try
            {
                using (var db = new DatabaseContext())
                {
                    var entry = db.Proxies.FirstOrDefault(x =>
                        x.Ip == value.Ip &&
                        x.Port == value.Port &&
                        x.Username == value.Username &&
                        x.Password == value.Password
                    );

                    if (entry == null)
                    {
                        db.Add(value);
                        db.SaveChanges();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        // PUT api/<ProxyController>/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] Proxy value)
        {
            try
            {
                using (var db = new DatabaseContext())
                {
                    var entry = db.Proxies.FirstOrDefault(x => x.Id == id);
                    if (entry != null)
                    {
                        entry.Ip = value.Ip;
                        entry.Port = value.Port;
                        entry.Username = value.Username;
                        entry.Password = value.Password;
                        entry.RealAddress = value.RealAddress;
                        db.SaveChanges();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        // DELETE api/<ProxyController>/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
            try
            {
                using (var db = new DatabaseContext())
                {
                    var entry = db.Proxies.FirstOrDefault(x => x.Id == id);
                    if (entry != null)
                    {
                        db.Proxies.Remove(entry);
                        db.SaveChanges();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}
