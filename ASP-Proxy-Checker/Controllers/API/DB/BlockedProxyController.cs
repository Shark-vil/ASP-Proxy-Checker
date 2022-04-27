using Microsoft.AspNetCore.Mvc;
using ProxyChecker.Database;
using ProxyChecker.Database.Models;

namespace ProxyChecker.Controllers.API.DB
{
    /// <summary>
    /// REST API для управления заблокированными прокси
    /// </summary>
    [Route("api/db/[controller]")]
    [ApiController]
    public class BlockedProxyController : ControllerBase
    {
        // GET: api/<BlockedProxyController>
        [HttpGet]
        public IEnumerable<BlockedProxy> Get()
        {
            try
            {
                using (var db = new DatabaseContext())
                {
                    return db.BlockedProxies.ToArray();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return null;
        }

        // GET api/<BlockedProxyController>/5
        [HttpGet("{id}")]
        public BlockedProxy Get(int id)
        {
            try
            {
                using (var db = new DatabaseContext())
                {
                    var entry = db.BlockedProxies.FirstOrDefault(x => x.Id == id);
                    if (entry != null) return entry;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return null;
        }

        // POST api/<BlockedProxyController>
        [HttpPost]
        public IActionResult Post([FromForm] string address)
        {
            try
            {
                string mask = "";
                string[] ipNumbers = address.Split(".");
                if (ipNumbers.Length < 3)
                    return StatusCode(400);

                for (int i = 0; i < 3; i++)
                {
                    mask += $"{ipNumbers[i]}";
                    if (i != 2) mask += ".";
                }

                using (var db = new DatabaseContext())
                {
                    var entry = db.BlockedProxies.FirstOrDefault(x =>
                        x.Ip == address && x.Mask == mask
                    );

                    if (entry == null)
                    {
                        db.Add(new BlockedProxy
                        {
                            Ip = address,
                            Mask = mask
                        });
                        db.SaveChanges();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return Ok();
        }

        // PUT api/<BlockedProxyController>/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] BlockedProxy value)
        {
            try
            {
                using (var db = new DatabaseContext())
                {
                    var entry = db.BlockedProxies.FirstOrDefault(x => x.Id == id);
                    if (entry != null)
                    {
                        entry = value;
                        db.SaveChanges();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        // DELETE api/<BlockedProxyController>/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
            try
            {
                using (var db = new DatabaseContext())
                {
                    var entry = db.BlockedProxies.FirstOrDefault(x => x.Id == id);
                    if (entry != null)
                    {
                        db.BlockedProxies.Remove(entry);
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
