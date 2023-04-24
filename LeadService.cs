using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.WebServiceClient;
using Newtonsoft.Json;
using Planeta.SARLAC_CRM_Integration.ApiNET6.DTOs;
using Planeta.SARLAC_CRM_Integration.ApiNET6.Enums;
using Planeta.SARLAC_CRM_Integration.ApiNET6.Extensions;
using Planeta.SARLAC_CRM_Integration.ApiNET6.Models;
using Planeta.SARLAC_CRM_Integration.ApiNET6.Services.Contracts;
using Planeta.SARLAC_CRM_Integration.ApiNET6.Utils;
namespace Planeta.SARLAC_CRM_Integration.ApiNET6.Services.Implementations
{
    /// <summary>
    /// 
    /// </summary>
    public class LeadService : ILeadService
    {
        /// <summary>
        /// 
        /// </summary>
        protected ILogger Logger;

        /// <summary>
        /// 
        /// </summary>
        private readonly HttpClientService _httpClientService;

        /// <summary>Dirección web del Lead con el que se está operando.</summary>
        private string _uri;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="loggerFactory"></param>
        /// <param name="httpClientService"></param>
        public LeadService(
            ILoggerFactory loggerFactory,
            HttpClientService httpClientService)
        {
            Logger = loggerFactory.CreateLogger(GetType().Namespace);
            _httpClientService = httpClientService;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="token"></param>
        /// <param name="leadId"></param>
        /// <param name="brand"></param>
        /// <returns></returns>
        public async Task<bool> ExistsAsync(
            string token,
            string leadId,
            string brand)
        {
            var query = string.Format(
                Constants.GetByIdFilter,
                Lookup.Lead.GetStringValue(),
                LookupField.Lead.GetStringValue(),
                leadId,
                LookupField.Marca.GetStringValue(),
                brand);

            var response = await _httpClientService.SendRequestAsync(
                token,
                HttpMethod.Get,
                query);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                var collection = JsonConvert.DeserializeObject<JObject>(response.Content.ReadAsStringAsync().Result);

                collection.TryGetValue("value", out JToken value);

                return value != null && ((JArray)value).Count == 1;
            }

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="token"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task<LeadPostResponse> CreateAsync(string token, JObject value)
        {
            try
            {
                var response = await _httpClientService.SendAsJsonAsync(
                    token,
                    Lookup.Lead.GetStringValue(),
                    value,
                    HttpMethod.Post);
                var responsecontent = response.Content.ReadAsStringAsync().Result;

                if (!String.IsNullOrEmpty(responsecontent)) Logger.LogError($"Error Dynamics365: {response.Content.ReadAsStringAsync().Result}");
                if (response.StatusCode == HttpStatusCode.NoContent)
                {
                    _uri = response.Headers.GetValues("OData-EntityId").FirstOrDefault();

                    return new LeadPostResponse { HttpStatus = HttpStatusCode.Created, UriLead = _uri };
                }
                else { throw new Exception($"Error creating the lead: {responsecontent}"); }
                //return new LeadPostResponse { HttpStatus = response.StatusCode, UriLead = null };
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }


        public async Task<LeadPostResponse> CreateAsync(IOrganizationService service, JObject value)
        {
            try
            {
                var entity = new Entity("lead");
                entity["subject"] = (string)value["subject"];
                entity["firstname"] = (string)value["firstname"];
                entity["lastname"] = (string)value["lastname"];
                entity["emailaddress1"] = (string)value["emailaddress1"];

                var response = service.Create(entity);

                if (response != Guid.Empty)
                {
                    return new LeadPostResponse { HttpStatus = HttpStatusCode.Created, UriLead = $"/api/data/v9.1/leads({response})" };
                }
                else
                {
                    throw new Exception($"Error creating the lead");
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="token"></param>
        /// <param name="uriLead"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task<LeadDto> UpdateLeadAsync(string token, string uriLead, JObject value)
        {
            HttpResponseMessage response = null;
            //var query = $"lead({idLead})";
            var query = uriLead;

            var content = new StringContent(value.ToString(), Encoding.UTF8, "application/json");

            try
            {
                response = await _httpClientService.SendRequestAsync(
                    token,
                    HttpMethod.Patch,
                    query, false, 10, content);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error setting lead: {0}. Message: {1}", uriLead, ex.ToString());
            }

            return response?.StatusCode != HttpStatusCode.OK
                ? null
                : JsonConvert.DeserializeObject<LeadDto>(response.Content.ReadAsStringAsync().Result);
        }

        public async Task<LeadPostResponse> GetAsync(string token, string query, string field)
        {
            try
            {
                var response = await _httpClientService.GetAsync(
                    token,
                    query);
                var responseContent = response.Content.ReadAsStringAsync().Result;

                if (response.Content.Headers.ContentLength != 0 && response.StatusCode == HttpStatusCode.OK)
                {
                    //_uri = response.Headers.GetValues("OData-EntityId").FirstOrDefault();
                    var json = JsonConvert.DeserializeObject<JObject>(responseContent);
                    var entityUris = String.Empty;
                    string value = String.Empty;

                    foreach (JObject contactRef in json["value"])
                    {
                        //Add to the top of the list so these are deleted first
                        var test = ((Newtonsoft.Json.Linq.JValue)contactRef[field]).Value;
                        value = test.ToString();
                        //entityUris.Insert(0, new Uri(contactRef["@odata.id"].ToString());
                    }
                    if (value != String.Empty)
                    {
                        return new LeadPostResponse { HttpStatus = HttpStatusCode.Found, UriLead = null, D365Guid = value };
                    }
                }

                return new LeadPostResponse { HttpStatus = HttpStatusCode.NotFound, UriLead = null };
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public async Task<JObject> FillCRMFields(JObject json, string token, ProfileDto profile)
        {
            string institutionId = String.Empty;
            string campusId = String.Empty;
            string programaId = String.Empty;
            string tipoDocId = String.Empty;
            string situacionLaboralId = String.Empty;
            string paisId = String.Empty;
            var institutionOwnerId = String.Empty;
            bool isNormalized = true;
            var common = new Common();
            var leadNormalizationInfo = new StringBuilder();

            var crmUrl = Environment.GetEnvironmentVariable("CrmUrl");
            string crmURL = $"{crmUrl}api/data/v9.2/";

            try
            {
                json.Add("firstname", profile.Lead?.Nombre);
                json.Add("lastname", profile.Lead?.PrimerApellido);
                json.Add("middlename", profile.Lead?.SegundoApellido);
                json.Add("emailaddress1", profile.Lead?.Email);
                json.Add("address1_telephone1", profile.Lead?.Telefono1);
                json.Add("telephone1", profile.Lead?.Telefono2);
                json.Add("mcs_governmentid", profile.Lead?.Nif);
                json.Add("address1_postalcode", profile.Lead?.CodigoPostal);
                json.Add("mcs_address1streetnumber", profile.Lead?.Numero);
                json.Add("msdyncrm_leadid", profile.Key?.Id);
                json.Add("mcs_professionalsector", profile.Laboral?.Actividad);
                json.Add("jobtitle", profile.Laboral?.Cargo);
                json.Add("mcs_professionalexperienceyears", profile.Laboral?.AnyosDeExperiencia);
                json.Add("companyname", profile.Laboral?.Empresa);
                json.Add("mcs_origin", 803750000); //Website Integration
                json.Add("address1_line1", profile.Lead?.DomicilioFormatted);
                json.Add("mcs_privacypolicyconsent", profile.RGPD?.RGPD);
                json.Add("mcs_commercialcommunicationconsent", profile.RGPD?.RGPD1);
                json.Add("mcs_datacommunicationconsent", profile.RGPD?.RGPD2);
                json.Add("mcs_datosdeintereseditable", profile.Estudio?.DatosInteres);
                json.Add("mcs_otrosestudioseditable", profile.Estudio?.OtrosEstudios);
                json.Add("mcs_applicationdate", profile.Key?.FechaSolicitudFormatted);
                json.Add("mcs_functions", profile.Laboral?.Funciones);
                json.Add("mcs_peopleincharge", (int?)profile.Laboral?.PersonasACargo);
                json.Add("mcs_careergoals", profile.Laboral?.ObjetivosProfesionales);
                json.Add("mcs_observations", profile.Laboral?.Observaciones);
                json.Add("mcs_contacttime", profile.Marketing?.Horario);
                json.Add("mcs_campaignurl", profile.Marketing?.Url);
                json.Add("mcs_ip", profile.Marketing?.IP);
                json.Add("mcs_choiceexplanation", profile.Programa?.EleccionPrograma);
                json.Add("mcs_scoreintegration", profile.LeadRating?.Score);
                json.Add("mcs_ratingintegration", profile.LeadRating?.Rating);


                if (profile.RGPD?.RGPD != null)
                {
                    json.Add("donotbulkemail", !profile.RGPD.RGPD);
                    json.Add("donotemail", !profile.RGPD.RGPD);
                }
                //json.Add("mcs_json", JsonConvert.SerializeObject(profile));

                if (profile.Key.Marca != null)
                {
                    #region Marca
                    try
                    {
                        string query = crmURL + "sis_institutions?$select=sis_institutionid&$filter=(sis_institutioncode eq '" + profile.Key.Marca + "' and statecode eq 0)&$top=1";
                        var response = await GetAsync(token, query, "sis_institutionid");
                        if (response.HttpStatus == HttpStatusCode.Found)
                        {
                            institutionId = response.D365Guid;
                            json.Add("mcs_institutionid@odata.bind", "/sis_institutions(" + institutionId + ")");

                            string queryOwner = crmURL + "sis_institutions?$select=_ownerid_value&$filter=(sis_institutionid eq '" + institutionId + "' and statecode eq 0)&$top=1";
                            var responseOwner = await GetAsync(token, queryOwner, "_ownerid_value");
                            if (responseOwner.HttpStatus == HttpStatusCode.Found)
                            {
                                institutionOwnerId = responseOwner.D365Guid;
                            }
                        }
                        else
                        {
                            isNormalized = false;
                            leadNormalizationInfo.AppendLine($"The institution {profile.Key.Marca} could not be found");
                        }
                    }
                    catch (Exception ex)
                    {
                        isNormalized = false;
                        leadNormalizationInfo.AppendLine($"The institution {profile.Key.Marca} could not be found");
                        leadNormalizationInfo.AppendLine($"Exception Message: {ex.Message}");
                    }
                    
                    #endregion

                    #region Campus
                    if (profile.Programa?.Campus != null)
                    {
                        try
                        {
                            string querycampus = crmURL + "sis_campuses?$select=sis_campusid&$filter=(_sis_institutionid_value eq '" + institutionId + "' and sis_campuscode eq '" + profile.Programa.Campus + "' and statecode eq 0)&$top=1";
                            var response = await GetAsync(token, querycampus, "sis_campusid");
                            if (response.HttpStatus == HttpStatusCode.Found)
                            {
                                campusId = response.D365Guid;
                                json.Add("mcs_CampusId@odata.bind", "/sis_campuses(" + campusId + ")");
                            }
                            else
                            {
                                isNormalized = false;
                                leadNormalizationInfo.AppendLine($"The campus {profile.Programa.Campus} could not be found");
                            }
                        }
                        catch(Exception ex)
                        {
                            isNormalized = false;
                            leadNormalizationInfo.AppendLine($"The campus {profile.Programa.Campus} could not be found");

                        }
                    }
                    #endregion
                    #region Programa
                    if (profile.Programa?.Nombre != null)
                    {
                        try
                        {
                            string queryprogrmaversion = crmURL + "mshied_programversions?$select=mshied_programversionid&$filter=(mcs_webcode eq '" + profile.Programa.Nombre + "' and _ownerid_value eq '" + institutionOwnerId + "' and statecode eq 0)&$top=1";
                            var response = await GetAsync(token, queryprogrmaversion, "mshied_programversionid");
                            if (response.HttpStatus == HttpStatusCode.Found)
                            {
                                programaId = response.D365Guid;
                                json.Add("mcs_ProgramVersionId@odata.bind", "/mshied_programversions(" + programaId + ")");
                                string queryprogrma = crmURL + "mshied_programversions?$select=_mshied_programid_value&$filter=(mshied_programversionid eq '" + programaId + "' and _ownerid_value eq '" + institutionOwnerId + "' and statecode eq 0)&$top=1";
                                response = await GetAsync(token, queryprogrma, "_mshied_programid_value");
                                if (response.HttpStatus == HttpStatusCode.Found)
                                {
                                    programaId = response.D365Guid;
                                    json.Add("mcs_programid@odata.bind", "/mshied_programs(" + programaId + ")");
                                }
                            }
                            else
                            {
                                string queryprogrma = crmURL + "mshied_programs?$select=mshied_programid&$filter=(mcs_webcode eq '" + profile.Programa.Nombre + "' and _ownerid_value eq '" + institutionOwnerId + "' and statecode eq 0)&$top=1";
                                response = await GetAsync(token, queryprogrma, "mshied_programid");
                                if (response.HttpStatus == HttpStatusCode.Found)
                                {
                                    programaId = response.D365Guid;
                                    json.Add("mcs_programid@odata.bind", "/mshied_programs(" + programaId + ")");
                                }
                                else
                                {
                                    isNormalized = false;
                                    leadNormalizationInfo.AppendLine($"The program {profile.Programa.Nombre} could not be found");
                                }
                            }
                        }
                        catch(Exception ex)
                        {
                            isNormalized = false;
                            leadNormalizationInfo.AppendLine($"The program {profile.Programa.Nombre} could not be found");
                        }
                    }
                    #endregion
                    #region TipoDocumento
                    if (profile.Lead.TipoDocumento != null)
                    {
                        try
                        {
                            var tipoDoc = common.TipoDocumento.ContainsKey(int.Parse(profile.Lead.TipoDocumento)) ? common.TipoDocumento[int.Parse(profile.Lead.TipoDocumento)] : null;
                            if (tipoDoc != null)
                            {
                                string queryTipoDoc = crmURL + "sis_governmentdocumenttypes?$select=sis_governmentdocumenttypeid&$filter=(sis_uxxicode eq '" + tipoDoc + "' and statecode eq 0)&$top=1";
                                var response = await GetAsync(token, queryTipoDoc, "sis_governmentdocumenttypeid");
                                if (response.HttpStatus == HttpStatusCode.Found)
                                {
                                    tipoDocId = response.D365Guid;
                                    json.Add("mcs_GovernmentDocumentTypeId@odata.bind", "/sis_governmentdocumenttypes(" + tipoDocId + ")");
                                }
                                else
                                {
                                    isNormalized = false;
                                    leadNormalizationInfo.AppendLine($"The government document type {profile.Lead.TipoDocumento} could not be found");
                                }
                            }
                        }
                        catch(Exception ex)
                        {
                            isNormalized = false;
                            leadNormalizationInfo.AppendLine($"The government document type {profile.Lead.TipoDocumento} could not be found");
                        }
                    }
                    #endregion
                    #region Situacion Laboral
                    if (profile.Laboral?.Situacion != null)
                    {
                        try
                        {
                            string querySituacionLaboral = crmURL + "mcs_professionalsituations?$select=mcs_professionalsituationid&$filter=(mcs_id eq '" + profile.Laboral.Situacion + "' and statecode eq 0)&$top=1";
                            var response = await GetAsync(token, querySituacionLaboral, "mcs_professionalsituationid");
                            if (response.HttpStatus == HttpStatusCode.Found)
                            {
                                situacionLaboralId = response.D365Guid;
                                json.Add("mcs_professionalsituation@odata.bind", "/mcs_professionalsituations(" + situacionLaboralId + ")");
                            }
                            else
                            {
                                isNormalized = false;
                                leadNormalizationInfo.AppendLine($"The professional situation {profile.Laboral.Situacion} could not be found");
                            }
                        }
                        catch(Exception ex)
                        {
                            isNormalized = false;
                            leadNormalizationInfo.AppendLine($"The professional situation {profile.Laboral.Situacion} could not be found");
                        }
                    }
                    #endregion
                    #region Address

                    json.Add("address1_country", profile.Lead.PaisISO);
                    if (profile.Lead.PaisISO != null)
                    {
                        try
                        {
                            string queryPais = crmURL + "sis_countrieses?$select=sis_countriesid&$filter=(sis_iso2 eq '" + profile.Lead.PaisISO + "' and statecode eq 0)&$top=1";
                            var response = await GetAsync(token, queryPais, "sis_countriesid");
                            if (response.HttpStatus == HttpStatusCode.Found)
                            {
                                paisId = response.D365Guid;
                                json.Add("mcs_address1countryid@odata.bind", "/sis_countrieses(" + paisId + ")");
                                //if (profile.Lead.ProvinciaISO!= null)
                                //{
                                //    string  queryProvincia = crmURL + "sis_states?$select=sis_name&$filter=(_sis_countryid_value eq " + paisId +" and sis_statecode eq '" + profile.Lead.ProvinciaISO + "' and statecode eq 0)&$top=1";
                                //    response = await GetAsync(token, queryProvincia, "sis_name");
                                //    if(response.HttpStatus == HttpStatusCode.Found)
                                //    {
                                //        json.Add("mcs_address1_stateorprovince", response.D365Guid);
                                //        json.Add("address1_stateorprovince", profile.Lead.ProvinciaISO);
                                //    }
                                //    else
                                //    {
                                //        isNormalized = false;
                                //        leadNormalizationInfo.AppendLine($"The state or province {profile.Lead.ProvinciaISO} could not be found");
                                //    }
                                //}
                                json.Add("mcs_address1_stateorprovince", profile.Lead.ProvinciaNoNormalizada);
                                json.Add("address1_stateorprovince", profile.Lead.ProvinciaNoNormalizada);
                                json.Add("address1_city", profile.Lead.Poblacion);
                            }
                            else
                            {
                                isNormalized = false;
                                leadNormalizationInfo.AppendLine($"The country {profile.Lead.PaisISO} could not be found");
                            }
                        }
                        catch(Exception ex)
                        {
                            isNormalized = false;
                            leadNormalizationInfo.AppendLine($"The country {profile.Lead.PaisISO} could not be found");
                        }
                        
                    }

                    #endregion
                    #region Study

                    if (profile.Estudio?.Nivel != null)
                    {
                        try
                        {
                            string genericQuery = crmURL + "mcs_levelofstudies?$select=mcs_levelofstudyid&$filter=(mcs_code eq '" + profile.Estudio.Nivel + "' and _mcs_institutionid_value eq " + institutionId + " and statecode eq 0)&$top=1";
                            var response = await GetAsync(token, genericQuery, "mcs_levelofstudyid");
                            if (response.HttpStatus == HttpStatusCode.Found)
                            {
                                json.Add("mcs_LevelofStudyId@odata.bind", "/mcs_levelofstudies(" + response.D365Guid + ")");
                            }
                            else
                            {
                                isNormalized = false;
                                leadNormalizationInfo.AppendLine($"The level of studies {profile.Estudio.Nivel} could not be found");
                            }
                        }
                        catch(Exception ex)
                        {
                            isNormalized = false;
                            leadNormalizationInfo.AppendLine($"The level of studies {profile.Estudio.Nivel} could not be found");
                        }
                    }
                    json.Add("msdyncrm_school", profile.Estudio.Centro);
                    json.Add("msdyncrm_fieldofstudy", profile.Estudio.Especialidad);
                    json.Add("mcs_laststudiesfinished", profile.Estudio?.AnyoFinEstudiosAcademico?.ToString());

                    if (profile.Estudio?.NivelDeIngles != null)
                    {
                        try
                        {
                            string genericQuery = crmURL + "mcs_languajelevels?$select=mcs_languajelevelid&$filter=(mcs_code eq '" + profile.Estudio.NivelDeIngles + "' and _mcs_institutionid_value eq " + institutionId + " and statecode eq 0)&$top=1";
                            var response = await GetAsync(token, genericQuery, "mcs_languajelevelid");
                            if (response.HttpStatus == HttpStatusCode.Found)
                            {
                                json.Add("mcs_EnglishLevelId@odata.bind", "/mcs_languajelevels(" + response.D365Guid + ")");
                            }
                            else
                            {
                                isNormalized = false;
                                leadNormalizationInfo.AppendLine($"The english level {profile.Estudio.NivelDeIngles} could not be found");
                            }
                        }
                        catch (Exception ex)
                        {
                            isNormalized = false;
                            leadNormalizationInfo.AppendLine($"The english level {profile.Estudio.NivelDeIngles} could not be found");
                        }
                    }
                    if (profile.Estudio?.NivelDeFrances != null)
                    {
                        try
                        {
                            string genericQuery = crmURL + "mcs_languajelevels?$select=mcs_languajelevelid&$filter=(mcs_code eq '" + profile.Estudio.NivelDeFrances + "' and _mcs_institutionid_value eq " + institutionId + " and statecode eq 0)&$top=1";
                            var response = await GetAsync(token, genericQuery, "mcs_languajelevelid");
                            if (response.HttpStatus == HttpStatusCode.Found)
                            {
                                json.Add("mcs_FrenchLevelId@odata.bind", "/mcs_languajelevels(" + response.D365Guid + ")");
                            }
                            else
                            {
                                isNormalized = false;
                                leadNormalizationInfo.AppendLine($"The french level {profile.Estudio.NivelDeFrances} could not be found");
                            }
                        }
                        catch (Exception ex)
                        {
                            isNormalized = false;
                            leadNormalizationInfo.AppendLine($"The french level {profile.Estudio.NivelDeFrances} could not be found");
                        }
                    }
                    #endregion
                    #region Campanya
                    if (profile.Marketing?.Campanya != null)
                    {
                        try
                        {
                            string querycampnya = crmURL + "campaigns?$select=campaignid&$filter=(codename eq '" + profile.Marketing.Campanya + "' and _mcs_brandid_value eq " + institutionId + ")&$orderby=statecode asc";
                            var response = await GetAsync(token, querycampnya, "campaignid");
                            if (response.HttpStatus == HttpStatusCode.Found)
                            {
                                json.Add("mcs_campaign@odata.bind", "/campaigns(" + response.D365Guid + ")");
                                json.Add("campaignid@odata.bind", "/campaigns(" + response.D365Guid + ")");
                            }
                            else
                            {
                                isNormalized = false;
                                leadNormalizationInfo.AppendLine($"The marketing campaign {profile.Marketing.Campanya} could not be found");
                            }
                        }
                        catch(Exception ex)
                        {
                            isNormalized = false;
                            leadNormalizationInfo.AppendLine($"The marketing campaign {profile.Marketing.Campanya} could not be found");
                        }
                    }
                    #endregion
                    #region Proveedor
                    if (profile.Key.Proveedor != null)
                    {
                        try
                        {
                            string queryprovider = crmURL + "mcs_providers?$select=mcs_providerid&$filter=(mcs_code eq '" + profile.Key.Proveedor + "' and statecode eq 0)";
                            var response = await GetAsync(token, queryprovider, "mcs_providerid");
                            if (response.HttpStatus == HttpStatusCode.Found)
                            {
                                json.Add("mcs_leadprovider@odata.bind", "/mcs_providers(" + response.D365Guid + ")");
                            }
                            else
                            {
                                isNormalized = false;
                                leadNormalizationInfo.AppendLine($"The provider {profile.Key.Proveedor} could not be found");
                            }
                        }
                        catch(Exception ex)
                        {
                            isNormalized = false;
                            leadNormalizationInfo.AppendLine($"The provider {profile.Key.Proveedor} could not be found");
                        }
                    }
                    #endregion
                    #region CalculoEdad
                    if (profile.Lead.FechaNacimiento != null)
                    {
                        int age = 0;
                        DateTime fechaNacimiento = DateTime.Parse(profile.Lead.FechaNacimiento);
                        age = DateTime.Now.Subtract(fechaNacimiento).Days;
                        age = age / 365;
                        json.Add("mcs_leadage", age);

                    }
                    #endregion
                    #region Tipo
                    if (profile.Key.Tipo != null)
                    {
                        try
                        {
                            if (profile.Key.Tipo.ToUpper() == "SI")
                            {
                                json.Add("mcs_type", false);
                            }
                            else if (profile.Key.Tipo.ToUpper() == "SA")
                            {
                                json.Add("mcs_type", true);
                            }
                            else
                            {
                                isNormalized = false;
                                leadNormalizationInfo.AppendLine($"The Type {profile.Key.Tipo} could not be found");
                            }
                        }
                        catch(Exception ex)
                        {
                            isNormalized = false;
                            leadNormalizationInfo.AppendLine($"The Type {profile.Key.Tipo} could not be found");
                        }
                    }
                    #endregion
                    #region Lenguaje
                    if (profile.Key.Idioma != null)
                    {
                        try
                        {
                            string queryLanguage = crmURL + "sis_languages?$select=sis_languageid&$filter=(sis_isocode eq '" + profile.Key.Idioma + "' and statecode eq 0)&$top=1";
                            var response = await GetAsync(token, queryLanguage, "mcs_providerid");
                            if (response.HttpStatus == HttpStatusCode.Found)
                            {
                                json.Add("mcs_Languaje@odata.bind", "/sis_languages(" + response.D365Guid + ")");
                            }
                            else
                            {
                                isNormalized = false;
                                leadNormalizationInfo.AppendLine($"The languaje {profile.Key.Idioma} could not be found");
                            }
                        }
                        catch (Exception ex)
                        {
                            isNormalized = false;
                            leadNormalizationInfo.AppendLine($"The languaje {profile.Key.Idioma} could not be found");
                        }
                    }
                    #endregion
                    #region LeadOrigin
                    if (profile.Marketing?.Origen != null)
                    {
                        try
                        {
                            int origen = int.Parse(profile.Marketing.Origen);
                            switch (origen)
                            {
                                case 0: //Manual
                                    json.Add("mcs_leadorigin", 803750000);
                                    break;
                                case 1: //Migracion
                                    json.Add("mcs_leadorigin", 803750001);
                                    break;
                                case 2: //CSV
                                    json.Add("mcs_leadorigin", 803750002);
                                    break;
                                case 3: //Web
                                    json.Add("mcs_leadorigin", 803750003);
                                    break;
                                case 4: //Greedo
                                    json.Add("mcs_leadorigin", 803750004);
                                    break;
                                default:
                                    isNormalized = false;
                                    leadNormalizationInfo.AppendLine($"The lead origin {profile.Marketing.Origen} could not be found");
                                    break;
                            }
                        }
                        catch(Exception ex)
                        {
                            isNormalized = false;
                            leadNormalizationInfo.AppendLine($"The lead origin {profile.Marketing.Origen} could not be found");
                        }
                    }
                    #endregion
                    #region Dedication
                    if (profile.Programa.Dedicacion != null)
                    {
                        try
                        {
                            string queryDedication = crmURL + "mcs_config_loads?$select=mcs_load&$filter=(_mcs_institution_value eq " + institutionId + " and mcs_webcode eq " + int.Parse(profile.Programa.Dedicacion) + " and statecode eq 0)&$top=1";
                            var response = await GetAsync(token, queryDedication, "mcs_load");
                            if (response.HttpStatus == HttpStatusCode.Found)
                            {
                                json.Add("mcs_dedication", response.D365Guid);
                            }
                            else
                            {
                                isNormalized = false;
                                leadNormalizationInfo.AppendLine($"The dedication {profile.Programa.Dedicacion} could not be found");
                            }
                        }
                        catch(Exception ex)
                        {
                            isNormalized = false;
                            leadNormalizationInfo.AppendLine($"The dedication {profile.Programa.Dedicacion} could not be found");
                        }
                    }
                    #endregion
                    #region Impartation Type
                    if (profile.Programa.TipoDeImparticion != null)
                    {
                        try
                        {
                            string queryImpartation = crmURL + "mcs_config_modes?$select=mcs_mode&$filter=(_mcs_institution_value eq " + institutionId + " and mcs_webcode eq " + int.Parse(profile.Programa.TipoDeImparticion) + " and statecode eq 0)&$top=1";
                            var response = await GetAsync(token, queryImpartation, "mcs_mode");
                            if (response.HttpStatus == HttpStatusCode.Found)
                            {
                                json.Add("mcs_impartationtype", response.D365Guid);
                            }
                            else
                            {
                                isNormalized = false;
                                leadNormalizationInfo.AppendLine($"The impartation type {profile.Programa.TipoDeImparticion} could not be found");
                            }
                        }
                        catch(Exception ex)
                        {
                            isNormalized = false;
                            leadNormalizationInfo.AppendLine($"The impartation type {profile.Programa.TipoDeImparticion} could not be found");
                        }
                    }
                    #endregion
                    json.Add("mcs_notnormalized", !isNormalized);
                    json.Add("mcs_normalizationinfo", leadNormalizationInfo.ToString());
                }
                else
                {
                    leadNormalizationInfo.AppendLine($"The profile->Key->Marca could not be found");
                    json.Add("mcs_notnormalized", true);
                    json.Add("mcs_normalizationinfo", leadNormalizationInfo.ToString());
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"LEAD EMAIL: {profile.Lead.Email} - FillCRMFields - Error retrieving associated information - {ex.Message}");
            }
            return json;
        }

        public async Task<JObject> RetrieveInstitution(string token, ProfileDto profile, JObject json) {
            var crmUrl = Environment.GetEnvironmentVariable("CrmUrl");
            string crmURL = $"{crmUrl}api/data/v9.2/";

            string query = crmURL + "sis_institutions?$select=sis_institutionid&$filter=(sis_institutioncode eq '" + profile.Key.Marca + "' and statecode eq 0)&$top=1";
            var response = await GetAsync(token, query, "sis_institutionid");
            if (response.HttpStatus == HttpStatusCode.Found)
            {
                var institutionId = response.D365Guid;
                json.Add("mcs_institutionid@odata.bind", "/sis_institutions(" + institutionId + ")");
            }
            return json;
        }

        public async Task<bool> ExistLeadWithJSON(string token, string json)
        {
            try
            {
                var crmUrl = Environment.GetEnvironmentVariable("CrmUrl");
                string query = $"{crmUrl}api/data/v9.2/leads?$filter=(mcs_json eq '{json}')&$top=1";

                var response = await GetAsync(token, query, "mcs_json");
                if (response.HttpStatus == HttpStatusCode.Found)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch(Exception ex)
            {
                return false;
            }
            
        }
    }
}
