﻿using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace LinkShorter.Controllers
{
    [ApiController]
    [Route("/{shortPath}")]
    public class ShortPathController
    {
        private readonly DatabaseWrapper _databaseWrapper;

        public ShortPathController(DatabaseWrapper databaseWrapper)
        {
            this._databaseWrapper = databaseWrapper;
        }

        [HttpGet]
        public ActionResult Get(string shortPath)
        {
            var queryTargetUrl =
                @$"SELECT targeturl FROM links WHERE shortPath = '{shortPath}'; UPDATE links set clickcounter = clickcounter+1  WHERE shortPath = '{shortPath}'; ";
            var cmdUserId = new NpgsqlCommand(queryTargetUrl, _databaseWrapper.GetDatabaseConnection());

            var targeturl = cmdUserId.ExecuteScalar()?.ToString();

            if (targeturl == null)
            {
                return new NotFoundResult();
            }


            return new RedirectResult(targeturl);
        }
    }
}