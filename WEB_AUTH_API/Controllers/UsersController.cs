using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Data;
using WEB_AUTH_API.DataAccess;

namespace WEB_AUTH_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly DataHandeler _dataHandler;

        public UsersController(DataHandeler dataHandler)
        {
            _dataHandler = dataHandler;
        }

        [HttpPost("ListAllUsers")]
        public async Task<JsonResult> ListAllUsers([FromBody] string TokenNo)
        {
            try
            {
                if (string.IsNullOrEmpty(TokenNo))
                {
                    return new JsonResult(new { error = "TokenNo is required" }) { StatusCode = StatusCodes.Status400BadRequest };
                }

                SqlParameter[] parameters = {
                    new SqlParameter("@TokenNo",TokenNo)
                };
                string OrgInfo = await Task.Run(() => _dataHandler.ReadToJson("dbListAllUser", parameters, CommandType.StoredProcedure));
                JArray jArray = (JArray)JsonConvert.DeserializeObject(OrgInfo);
                return new JsonResult(jArray);

            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
