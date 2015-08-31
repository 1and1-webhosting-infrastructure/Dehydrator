﻿using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Http;
using Dehydrator.Sample.Models;
using Dehydrator.WebApi;

namespace Dehydrator.Sample.Controllers
{
    [RoutePrefix("api/packages")]
    public class PackageController : AsyncCrudController<Package>
    {
        public PackageController(IRepository<Package> repository) : base(repository)
        {
        }

        [HttpPost, Route("test-data")]
        public async Task TestData()
        {
            var awesomeLib = Repository.Add(new Package
            {
                FriendlyName = "AwesomeLib"
            });

            var awesomeApp = Repository.Add(new Package
            {
                FriendlyName = "AwesomeApp",
                Dependencies = new List<Package> {new Package {Id = awesomeLib.Id}}
            });

            await Repository.SaveChangesAsync();
        }
    }
}