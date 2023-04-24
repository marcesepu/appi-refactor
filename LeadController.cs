using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Planeta.SARLAC_CRM_Integration.ApiNET6.Attributes;
using Planeta.SARLAC_CRM_Integration.ApiNET6.Core.Response;
using Planeta.SARLAC_CRM_Integration.ApiNET6.DTOs;
using Planeta.SARLAC_CRM_Integration.ApiNET6.Services.Contracts;
using Planeta.SARLAC_CRM_Integration.ApiNET6.Utils;
using System.Text.RegularExpressions;
using System;
using System.IO;
using System.Threading;
using static System.Net.Mime.MediaTypeNames;
using System.Text;
using System.Web;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Planeta.SARLAC_CRM_Integration.ApiNET6.Services.Implementations;

namespace Planeta.SARLAC_CRM_Integration.ApiNET6.Controllers
{
    /// <summary>
    /// 
    /// </summary>
    [Produces("application/json")]
    [Route("api/lead")]
    public class LeadController : Controller
    {
        /// <summary>
        /// 
        /// </summary>
        private readonly ILogger<LeadController> _logger;

        /// <summary>
        /// 
        /// </summary>
        private readonly IDataverseService _crmService;

        /// <summary>
        /// 
        /// </summary>
        private readonly ILeadService _leadService;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="crmService"></param>
        /// <param name="leadService"></param>
        public LeadController(
            ILogger<LeadController> logger,
            IDataverseService crmService, 
            ILeadService leadService)
        {
            _logger = Guard.ArgumentNotNullAndReturn(logger, "logger");
            _crmService = Guard.ArgumentNotNullAndReturn(crmService, "crmService");
            _leadService = Guard.ArgumentNotNullAndReturn(leadService, "leadService");
        }

        /// <summary>
        /// Creación de un Lead in Dynamics CRM.
        /// </summary>
        /// <param name="profile">Información de la solicitud.</param>
        [HttpPost]
        [ProducesResponseType(201)]
        [ProducesResponseType(409)]
        [ApiValidationFilter]
        public async Task<IActionResult> Post([FromBody]ProfileDto profile)
        {
            string token= "";
            //try
            //{
            //    _logger.LogInformation("Getting the token async...");
            //    token = await _crmService.GetTokenAsync();
            //    _logger.LogInformation("Finished the token async...");

            //}
            //catch (Exception ex)
            //{
            //    _logger.LogError($"Leads Integration - Dynamics Connection Error - {ex.Message}");
            //    return StatusCode(500, ex.Message);
            //}

            // Create a Dataverse service client using the default connection string.
            ServiceClient serviceClient = _crmService.GetService();

            JObject jObject = profile.ToJObject();
            //var receivedJson = jObject;

            //revisar cual es su funcionalidad, por lo que se ve, guarda el request en un campo
            var receivedJson = String.Empty;
            try
            {
                using (var reader = new StreamReader(Request.Body))
                {
                    reader.BaseStream.Seek(0, SeekOrigin.Begin);
                    receivedJson = reader.ReadToEnd();
                }
                _logger.LogError($"Leads Integration JSON - {receivedJson}");
                jObject.Add("mcs_json", receivedJson);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Leads Integration - Json Bad Format - {ex.Message}");
                return BadRequest(new ApiBadRequestResponse(ModelState));
            }

            try
            {
                var encoded = HttpUtility.UrlEncode(receivedJson).Replace("%5C", "");
                var ExistInCRM = await _leadService.ExistLeadWithJSON(token, encoded);
                if (ExistInCRM) 
                {
                    _logger.LogError($"Leads Integration - Lead Found in CRM");
                    return Created(string.Empty, new ApiCreatedResponse()); //OK 201
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Leads Integration - Error in Query finding Leads in CRM - {ex.Message}");
            }

            try
            {
                _logger.LogInformation($"Creating instance to Create.");
                var filledJson = await _leadService.FillCRMFields(jObject, token, profile);
                jObject = filledJson.ToObject<JObject>();
                _logger.LogInformation($"Created Instance");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Leads Integration - Error while formating JSON - Bad Request {new ApiBadRequestResponse(ModelState)}");
                return StatusCode(400, $"Error while formating JSON - {jObject["mcs_json"].ToString()}"); //400
            }

            try
            {
                _logger.LogInformation($"Creating Lead...");
                var result = await _leadService.CreateAsync(token, jObject);
                if (result.HttpStatus == HttpStatusCode.Created || result.HttpStatus == HttpStatusCode.OK)
                {
                    _logger.LogError($"Leads Integration - Lead Created!");
                    return Created(string.Empty, new ApiCreatedResponse()); //OK 201
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"Leads Integration - POST - Creating Lead...");
                try
                {
                    Thread.Sleep(10000);
                    var ExistInCRM = await _leadService.ExistLeadWithJSON(token, HttpUtility.UrlEncode(receivedJson).Replace("%5C", ""));
                    if (!ExistInCRM)
                    {
                        _logger.LogInformation($"Leads Integration - POST - Lead NOT Found in CRM");
                        var jsonField = new JObject {   { "mcs_json", receivedJson }, 
                                                        { "firstname", profile.Lead?.Nombre }, 
                                                        { "lastname", profile.Lead?.PrimerApellido }, 
                                                        { "middlename", profile.Lead?.SegundoApellido },
                                                        { "emailaddress1", profile.Lead?.Email }, 
                                                        { "mcs_type", profile.Key?.Tipo?.ToUpper() == "SI" ? false : true },
                                                        { "mcs_notnormalized", true },
                                                        { "mcs_normalizationinfo", "Creación desde la Api con Errores." } };

                        var json = await _leadService.RetrieveInstitution(token, profile, jsonField);
                        await _leadService.CreateAsync(token, json);
                        _logger.LogError($"Leads Integration - POST - First Catch Empty lead created {receivedJson}");
                        return Created(string.Empty, new ApiCreatedResponse()); //OK 201
                    }
                    else
                    {
                        _logger.LogError($"Leads Integration - POST - Lead Found in CRM");
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError($"Leads Integration - POST 2 - Lead NOT Found in CRM");
                    var jsonField = new JObject { { "mcs_json", receivedJson } };
                    await _leadService.CreateAsync(token, jsonField);
                    _logger.LogError($"Leads Integration POST 2 - Empty lead created {receivedJson}");
                    return Created(string.Empty, new ApiCreatedResponse()); //OK 201
                }
                _logger.LogError($"Leads Integration - LEAD: {profile.Lead?.Nombre} {profile.Lead?.PrimerApellido} {profile.Lead?.SegundoApellido} - {profile.Lead?.Email} - Post - Error lead {ex.Message}");
                return StatusCode(500, ex.Message);
            }
            _logger.LogError($"Leads Integration - Bad Request {new ApiBadRequestResponse(ModelState)}");
            return BadRequest(new ApiBadRequestResponse(ModelState));
        }
   
    }
}