﻿using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Npgsql;

namespace LinkShorter.Controllers
{
    [Controller]
    [Route("/api/login")]
    public class UserController : ControllerBase
    {
        private DatabaseWrapper _databaseWrapper;
        private PasswordManager _passwordManager;
        private readonly StringGenerator _stringGenerator;
        private readonly SessionManager _sessionManager;

        public UserController(DatabaseWrapper databaseWrapper, PasswordManager passwordManager,
            StringGenerator stringGenerator, SessionManager sessionManager)
        {
            this._databaseWrapper = databaseWrapper;
            this._passwordManager = passwordManager;
            this._stringGenerator = stringGenerator;
            this._sessionManager = sessionManager;
        }

        [HttpPost]
        [Route("login")]
        /// <response code="200">login ok</response>
        /// <response code="401">invalid userdata</response>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public ActionResult Login([FromBody] LoginData loginData)
        {
            if (!_databaseWrapper.isConnected()) _databaseWrapper.reconnect();

            var response = new JObject();
            if (!CheckIfUsernameExists(loginData.Username))
            {
                response["errorMessage"] = "user does not exits";
                return StatusCode(404, response.ToString());
            }

            // validate password
            // get salt
            var sqlQuerySalt = @$"SELECT salt, password, id FROM users WHERE username = @Username;";
            var querySalt = new NpgsqlCommand(sqlQuerySalt, _databaseWrapper.GetDatabaseConnection());
            querySalt.Parameters.AddWithValue("Username", loginData.Username);
            querySalt.Prepare();
            var result = querySalt.ExecuteReader();

            result.Read();

            var salt = result.GetString(0);
            var password = result.GetString(1);
            var userid = result.GetGuid(2).ToString();
            result.Close();
            Console.WriteLine("uuid:" + userid);

            var hashedUserPasswordInput = _passwordManager.Hash(loginData.Password, salt);

            // set cookie

            if (hashedUserPasswordInput.Equals(password))
            {
                // set cookies
                Response.Cookies.Append("session", _sessionManager.Register(userid));

                Response.Cookies.Append("session", _sessionManager.Register(userid));
                Console.WriteLine(response.ToString());

                return StatusCode(200, response.ToString());
            }
            else
            {
                response["errorMessage"] = "password does not match";
                return StatusCode(401, response.ToString());
            }
        }


        [HttpDelete]
        [Route("logout")]
        /// <response code="200">logout</response>
        /// <response code="401">invalid userdata</response>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public ActionResult Logout()
        {
            if (!_databaseWrapper.isConnected()) _databaseWrapper.reconnect();

            var response = new JObject();

            Request.Cookies.TryGetValue("session", out var sessionId);
            if (!_sessionManager.VerifySession(sessionId))
            {
                response["errorMessage"] = "user is not logged in";

                return StatusCode(401, response);
            }

            response["logoutSuccessful"] = _sessionManager.RemoveSession(sessionId);
            Response.Cookies.Delete("session");

            return StatusCode(200, response.ToString());
        }


        [HttpDelete]
        [Route("removeAccount")]
        /// <response code="200">account removed</response>
        /// <response code="401">invalid userdata</response>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public ActionResult RemoveAccount()
        {
            if (!_databaseWrapper.isConnected()) _databaseWrapper.reconnect();

            var response = new JObject();

            Request.Cookies.TryGetValue("session", out var sessionId);
            if (!_sessionManager.VerifySession(sessionId))
            {
                response["errorMessage"] = "user is not authenticated";

                return StatusCode(401, response);
            }

            var userId = _sessionManager.GetUserFromSessionId(sessionId);
            var userIdCast = Guid.Parse(userId);

            // remove all shortlinks
            var sqlQueryLinks =
                @$"DELETE FROM links WHERE creatoruuid = @userId;";
            var queryLinks = new NpgsqlCommand(sqlQueryLinks, _databaseWrapper.GetDatabaseConnection());
            queryLinks.Parameters.AddWithValue("userId", userIdCast);
            queryLinks.Prepare();
            queryLinks.ExecuteNonQuery();


            // remove account
            var sqlQueryAccount =
                @$"DELETE FROM users WHERE id = @userId;";
            var query = new NpgsqlCommand(sqlQueryAccount, _databaseWrapper.GetDatabaseConnection());
            query.Parameters.AddWithValue("userId", userIdCast);
            query.Prepare();
            query.ExecuteNonQuery();

            return StatusCode(200, response.ToString());
        }

        [HttpPatch]
        [Route("changepassword")]
        /// <response code="200">password changed</response>
        /// <response code="401">invalid userdata</response>
        /// <summary>
        ///     input model 
        ///     {
        ///        "password": "PASSWORD"
        ///     }
        ///
        /// </summary>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public ActionResult ChangePassword([FromBody] PasswordData passwordData)
        {
            if (!_databaseWrapper.isConnected()) _databaseWrapper.reconnect();

            var response = new JObject();

            Request.Cookies.TryGetValue("session", out var sessionId);
            if (!_sessionManager.VerifySession(sessionId))
            {
                response["errorMessage"] = "user is not authenticated";

                return StatusCode(401, response);
            }

            var userId = _sessionManager.GetUserFromSessionId(sessionId);

            var salt = _passwordManager.SaltGenerator();

            var hash = _passwordManager.Hash(passwordData.Password, salt);


            // remove all shortlinks
            var sqlQueryLinks =
                @$"UPDATE user SET password = @hash, salt = @salt WHERE @userId;";
            var queryLinks = new NpgsqlCommand(sqlQueryLinks, _databaseWrapper.GetDatabaseConnection());
            queryLinks.Parameters.AddWithValue("hash", hash);
            queryLinks.Parameters.AddWithValue("salt", salt);
            var userIdCast = Guid.Parse(userId);
            queryLinks.Parameters.AddWithValue("userId", userIdCast);
            queryLinks.Prepare();
            queryLinks.ExecuteNonQuery();


            return StatusCode(200, response.ToString());
        }

        [HttpPost]
        [Route("validatesession/{session}")]
        /// <response code="200">session ok</response>
        /// <response code="404">session not found</response>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult ValidateSession(string session)
        {
            var response = new JObject();
            Console.WriteLine("session: " + _sessionManager.VerifySession(session));
            if (session != null && _sessionManager.VerifySession(session))
            {
                response["message"] = "session still alive";

                return StatusCode(200, response.ToString());
            }
            else
            {
                response["errorMessage"] = "session is invalid";
                return StatusCode(404, response.ToString());
            }
        }

        [Route("getusername")]
        [HttpPost]
        /// <summary>
        ///     response model 
        ///     {
        ///        "name": "USERNAME"
        ///     }
        ///
        /// </summary>
        /// <response code="401">conflict </response>
        /// <response code="200">reg successfull</response>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public ActionResult GetUserName()
        {
            if (!_databaseWrapper.isConnected()) _databaseWrapper.reconnect();

            var response = new JObject();

            Request.Cookies.TryGetValue("session", out var sessionId);
            if (sessionId != null && _sessionManager.VerifySession(sessionId))
            {
                var userId = _sessionManager.GetUserFromSessionId(sessionId);

                Console.WriteLine(userId);

                var sqlQueryUserName = @$"SELECT username FROM users WHERE id = @userId;";
                var sqlResult = new NpgsqlCommand(sqlQueryUserName, _databaseWrapper.GetDatabaseConnection());
                var userIdCast = Guid.Parse(userId);
                sqlResult.Parameters.AddWithValue("userId", userIdCast);
                sqlResult.Prepare();

                var result = sqlResult.ExecuteReader();

                result.Read();

                var username = result.GetString(0);
                result.Close();
                _databaseWrapper.GetDatabaseConnection().Close();
                response["name"] = username;
                return StatusCode(200, response.ToString());
            }
            else
            {
                response["errorMessage"] = "user is not lodged in";
                return StatusCode(401, response.ToString());
            }
        }


        [Route("register")]
        [HttpPost]
        /// <response code="409">conflict </response>
        /// <response code="200">reg successfull</response>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public ActionResult Register([FromBody] LoginData loginData)
        {
            if (!_databaseWrapper.isConnected()) _databaseWrapper.reconnect();

            var response = new JObject();

            if (CheckIfUsernameExists(loginData.Username))
            {
                response["errorMessage"] = "username already in use, try another one";
                return StatusCode(409, response.ToString());
            }

            var salt = _passwordManager.SaltGenerator();

            var hash = _passwordManager.Hash(loginData.Password, salt);


            string apikey;
            while (true)
            {
                apikey = _stringGenerator.GenerateApiKey();
                if (!CheckIfDuplicateApikeyExists(apikey)) break;
            }


            var insert =
                @$"INSERT INTO users(id, username, password, salt, apikey) VALUES (DEFAULT, @username, @hash, @salt, @apikey);
                SELECT id FROM users WHERE username = @username;";
            var insertion = new NpgsqlCommand(insert, _databaseWrapper.GetDatabaseConnection());

            insertion.Parameters.AddWithValue("username", loginData.Username);
            insertion.Parameters.AddWithValue("hash", hash);
            insertion.Parameters.AddWithValue("salt", salt);
            insertion.Parameters.AddWithValue("apikey", apikey);

            insertion.Prepare();


            var result = insertion.ExecuteScalar();


            var resp = new HttpResponseMessage();

            Console.WriteLine("userid: " + result.ToString());

            Response.Cookies.Append("session", _sessionManager.Register(result.ToString()));

            return StatusCode(200, response.ToString());
        }


        [Route("getapikey")]
        [HttpGet]
        /// <summary>
        ///     response model 
        ///     {
        ///        "apikey": "APIKEY"
        ///     }
        ///
        /// </summary>
        /// <response code="401">not authenticated</response>
        /// <response code="200">sends back the apikey for the user</response>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetApiKey()
        {
            if (!_databaseWrapper.isConnected()) await _databaseWrapper.reconnect();
            

            var response = new JObject();

            Request.Cookies.TryGetValue("session", out var sessionId);
            if (sessionId != null && _sessionManager.VerifySession(sessionId))
            {
                var userId = _sessionManager.GetUserFromSessionId(sessionId);

                Console.WriteLine(userId);

                var sqlQueryUserName = @$"SELECT apikey FROM users WHERE id = @userId;";
                var sqlResult = new NpgsqlCommand(sqlQueryUserName, _databaseWrapper.GetDatabaseConnection());
                var userIdCast = Guid.Parse(userId);
                sqlResult.Parameters.AddWithValue("userId", userIdCast);
                await sqlResult.PrepareAsync();
                var result = await sqlResult.ExecuteReaderAsync();

                await result.ReadAsync();

                var username = result.GetString(0);
                await result.CloseAsync();
                await _databaseWrapper.GetDatabaseConnection().CloseAsync();
                response["apikey"] = username;
                return StatusCode(200, response.ToString());
            }
            else
            {
                response["errorMessage"] = "user is not lodged in";
                return StatusCode(401, response.ToString());
            }
        }

        private bool CheckIfUsernameExists(string username)
        {
            if (!_databaseWrapper.isConnected()) _databaseWrapper.reconnect();

            var checkDuplicates = @$"SELECT username FROM users WHERE username = @username LIMIT 1;";
            var cmdCheckDuplicates = new NpgsqlCommand(checkDuplicates, _databaseWrapper.GetDatabaseConnection());

            cmdCheckDuplicates.Parameters.AddWithValue("username", username);
            cmdCheckDuplicates.Prepare();

            var duplicates = cmdCheckDuplicates.ExecuteReader();

            var val = duplicates.Read();
            duplicates.Close();
            return val;
        }

        private bool CheckIfDuplicateApikeyExists(string apikey)
        {
            if (!_databaseWrapper.isConnected()) _databaseWrapper.reconnect();

            var checkDuplicates = @$"SELECT apikey FROM users WHERE apikey = @apikey LIMIT 1;";
            var cmdCheckDuplicates = new NpgsqlCommand(checkDuplicates, _databaseWrapper.GetDatabaseConnection());

            cmdCheckDuplicates.Parameters.AddWithValue("apikey", apikey);
            cmdCheckDuplicates.Prepare();

            var duplicates = cmdCheckDuplicates.ExecuteReader();
            var val = duplicates.Read();
            duplicates.Close();

            return val;
        }
    }
}