using Microsoft.AspNetCore.Mvc;
using ProxyChecker.Database;
using ProxyChecker.Database.Models;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace ProxyChecker.Controllers.API.DB
{
    /// <summary>
    /// REST API для управления API токенами сканера
    /// </summary>
    [Route("api/db/[controller]")]
    [ApiController]
    public class IPQualityScoreTokenController : ControllerBase
    {
        // GET: api/<IPQualityScoreTokenController>
        [HttpGet]
        public IEnumerable<IPQualityScoreToken> Get()
        {
            try
            {
                using (var db = new DatabaseContext())
                {
                    return db.IPQualityScoreTokens.ToArray();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return null;
        }

        // GET api/<IPQualityScoreTokenController>/5
        [HttpGet("{id}")]
        public IPQualityScoreToken Get(int id)
        {
            try
            {
                using (var db = new DatabaseContext())
                {
                    var entry = db.IPQualityScoreTokens.FirstOrDefault(x => x.Id == id);
                    if (entry != null) return entry;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return null;
        }

        // POST api/<IPQualityScoreTokenController>
        [HttpPost]
        public void Post([FromForm] string token)
        {
            try
            {
                using (var db = new DatabaseContext())
                {
                    var entry = db.IPQualityScoreTokens.FirstOrDefault(x =>
                        x.Token == token
                    );

                    if (entry == null)
                    {
                        db.Add(new IPQualityScoreToken
                        {
                            Token = token
                        });
                        db.SaveChanges();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        // PUT api/<IPQualityScoreTokenController>/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] IPQualityScoreToken value)
        {
            try
            {
                using (var db = new DatabaseContext())
                {
                    var entry = db.IPQualityScoreTokens.FirstOrDefault(x => x.Id == id);
                    if (entry != null)
                    {
                        entry.Token = value.Token;
                        db.SaveChanges();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        // DELETE api/<IPQualityScoreTokenController>/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
            try
            {
                using (var db = new DatabaseContext())
                {
                    var entry = db.IPQualityScoreTokens.FirstOrDefault(x => x.Id == id);
                    if (entry != null)
                    {
                        db.IPQualityScoreTokens.Remove(entry);
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
