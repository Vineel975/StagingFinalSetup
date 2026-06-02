using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.Mvc;
using System.Configuration;
using System.Data;
using log4net;
using log4net.Config;
using System.IO;
using System.IO.Compression;
using ClosedXML.Excel;
using System.Text;
using Newtonsoft.Json;
using Resources;
using Enrollment.ViewModel;
using Enrollment.Models;
using System.Collections.Specialized;
using EnrollmentDAL.Utilities;
using SpectraUtils;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.Net;
using System.Threading.Tasks;
using EnrollmentBAL;
using System.Net.Http.Headers;
using System.Net.Http;
using DAL;
using System.Reflection;
using System.Collections.ObjectModel;
using Newtonsoft.Json.Linq;
//using iTextSharp;
//using iTextSharp.text;
//using iTextSharp.text.pdf;
//using iTextSharp.tool.xml;
//using iTextSharp.tool.xml.pipeline.css;
//using iTextSharp.tool.xml.css;
//using iTextSharp.tool.xml.pipeline.html;
//using iTextSharp.tool.xml.pipeline.end;
//using iTextSharp.tool.xml.parser;
//using iTextSharp.tool.xml.html;


namespace Enrollment.Controllers
{
    [Authorize]
    public class MedicalScrutinyController : Controller
    {
        private int ClaimIRRoleID = 17;
        private int ClaimRejectionRoleID = 13;
        private int ClaimSettlementRoleID = 19;
        ILog logger = LogManager.GetLogger(typeof(MemberPortingController));
        private MedicalScrutinyViewModel _objMadicalScrutinyVM = new MedicalScrutinyViewModel();
        private ClaimsViewModel _objClaimsVM = new ClaimsViewModel();
        private byte StageId;
        private byte Slno;
        CommonController _objCommon = new CommonController();
        long VVflag = 0;
        DefaultCacheProvider cacheobj = new DefaultCacheProvider();
        //Ramesh Arjampudi Added QMS parameter to verify the Claims comes from Assigned Claims 17-11-2022 Start
        [Authorize]
        public ActionResult NavIndex(string ClaimID, string SlNo, string SID, string AID, string QMS = "", string QMSadmin = "")
        {
            TempData["ClaimID"] = ClaimID;
            TempData["SlNo"] = SlNo;
            TempData["SID"] = SID;
            TempData["AID"] = AID;
            TempData["QMS"] = QMS;
            //SP3V-2577
            TempData["QMSadmin"] = QMSadmin;
            //SP3V-2577
            //this.ClaimID = ClaimID;
            //this.SlNo = SlNo;
            //this.SID = SID;
            if (Session[SessionValue.UserRegionID] != null && Convert.ToInt32(Session[SessionValue.UserRegionID]) != 0)
            {
                ViewData["isFrmArchived"] = _objMadicalScrutinyVM.CheckClaimIsExistOrNot(Convert.ToInt64(ClaimID));
            }
            return RedirectToAction("Index");
        }
        //public ActionResult Index(string ClaimID, string SlNo, string SID)

        [Authorize]
        public ActionResult Index()
        {
            try
            {
                string ClaimID = string.Empty;
                string SlNo = string.Empty;
                string SID = string.Empty;
                string AID = string.Empty;
                //ClaimID = "3015";
                //SlNo = "1";
                //SID = "5";

                if (Session[SessionValue.UserRegionID] != null && Convert.ToInt32(Session[SessionValue.UserRegionID]) != 0)
                {
                    ViewData["LoginUserRoleID"] = Session[SessionValue.AllowRoleIDs];
                    ClaimID = Convert.ToString(TempData["ClaimID"]);
                    SlNo = Convert.ToString(TempData["SlNo"]);
                    SID = Convert.ToString(TempData["SID"]);
                    if (ClaimID != "" && SlNo != "" && SID != "")
                    {
                        TempData["ClaimID"] = ClaimID;
                        TempData["SlNo"] = SlNo;
                        TempData["SID"] = SID;
                        ViewData["AID"] = TempData["AID"];
                        TempData.Keep("ClaimID");
                        TempData.Keep("SlNo");
                        TempData.Keep("SID");
                        TempData.Keep("AID");
                        //Ramesh Arjampudi Assigned QMS parameter to View Bag to use in Javascript for updating the Status 17-11-2022 End
                        ViewData["QMS"] = TempData["QMS"];
                        TempData.Keep("QMS");
                        ViewData["QMSadmin"] = TempData["QMSadmin"];
                        TempData.Keep("QMSadmin");
                        //DMS Integration
                        ViewData["DMSApiURL"] = System.Web.Configuration.WebConfigurationManager.AppSettings["DMSApiURL"].ToString();
                        //DMS APIKey Authentication
                        string clientId = ConfigurationManager.AppSettings["ClientID"];
                        string DMSAPIKey = ConfigurationManager.AppSettings["DMSAPIKey"];
                        string clearText = clientId + "|" + DMSAPIKey;
                        ViewData["qString"] = new MasterUtilsBL().Encrypt(clearText, Convert.ToString(ConfigurationManager.AppSettings["URLEncryptionKey"]));
                        string ReferToInsurerIssueIds = Convert.ToString(ConfigurationManager.AppSettings["OnAuditReferToInsurer"]);
                        ViewData["ReferToInsurerIssueIds"] = ReferToInsurerIssueIds;
                        //SP3V-2595-Alert message for BOB Policies at Adjudication Stage
                        ViewData["ValidateBOBPolicies"] = ConfigurationManager.AppSettings["ValidateBOBPolicies"];

                        //Ramesh Arjampudi Assigned QMS parameter to View Bag to use in Javascript for updating the Status 17-11-2022 End
                        if (_objCommon.IsValidRole(new int[] { 12, 20, 13, 14, 15, 16, 17, 18, 19, 32, 21, 56 }, (int[])Session["AllowedRoles"]))
                        {
                            if (cacheobj.IsSet(ClaimID, Convert.ToInt32(Session[SessionValue.LoginUserID])))
                            {
                                CacheProvider cache = (CacheProvider)cacheobj.Get(ClaimID);
                                ViewData["ErrorMsg"] = "Claim locked by " + cache.UserName + "";
                                return View();
                            }
                            else
                            {


                                if (cacheobj.IsUserHaveLocks(Convert.ToInt32(Session[SessionValue.LoginUserID]), ClaimID, CacheItemType.ClaimLock))
                                {
                                    ViewData["ErrorMsg"] = "You have already locked Other Claim. Please unlock the Previous Claim to process another claim ";
                                    return View();
                                }
                                else
                                {
                                    CacheProvider cache = new CacheProvider();
                                    //Enrollment.Models.DefaultCacheProvider.Cacheobject cache = new DefaultCacheProvider.Cacheobject();
                                    cache.UserID = Convert.ToInt32(Session[SessionValue.LoginUserID]);
                                    ViewData["LoginUserID"] = Session[SessionValue.LoginUserID];
                                    ViewData["UserRegionID"] = Session[SessionValue.UserRegionID];

                                    cache.UserName = User.Identity.Name;
                                    cache.CachedDatetime = DateTime.Now;
                                    cache.CacheitemType = CacheItemType.ClaimLock;
                                    new DefaultCacheProvider().Set(ClaimID, cache, int.MaxValue);
                                    // ViewData["AppLive"] = Session[SessionValue.AppLive];
                                    //ViewData["AppURL"] = Session[SessionValue.AppURL];

                                    // Added for Spectra-iAI integration (SP3V-4924)
                                    ViewData["LoginUserName"] = Session[SessionValue.LoginUserName];
                                    ViewData["UserRoleID"] = Session[SessionValue.UserRoleID];
                                    ViewData["UserDepartmentID"] = Session[SessionValue.UserDepartmentID];
                                    ViewData["LoginType"] = Session[SessionValue.LoginType];
                                    //ViewData["iAIRedirectionURL"] = Convert.ToString(ConfigurationManager.AppSettings["iAIRedirectionURL"]);
                                    // End (SP3V-4924)

                                    LoadBasicData(ClaimID, SlNo, SID, ViewData["QMS"].ToString(), false, ViewData["QMSadmin"].ToString());
                                    ViewData["ErrorMsg"] = "";
                                    return View();
                                }
                            }
                        }
                        else
                        {
                            //TempData["ErrorMsg"] = "You do not have permissions for this page to access";
                            // return RedirectToAction("AccessDenied");
                            return View("AccessDenied");
                        }

                    }
                    else
                    {
                        return RedirectToAction("Index", "Claims");
                    }

                }

                else
                {
                    return RedirectToAction("MCareLogin", "Account");
                }

            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "Load", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                throw ex;
            }
        }

        public string ClaimConfiguration_Insert(string values)
        {
            try
            {
                string Msg = string.Empty;
                DataTable dt = new DataTable();
                dt = (DataTable)JsonConvert.DeserializeObject(values, (typeof(DataTable)));
                _objMadicalScrutinyVM.ClaimConfiguration_Insert(ref dt, Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]), out Msg);

                return Msg;
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        //SP3V-1432 START
        [HttpPost]
        public string Checkpreviousmaternity(string Action, string claimid, string slno, string Uhidno)
        {

            DataTable maternitydt = null;
            maternitydt = _objMadicalScrutinyVM.maternitysearch(Action, claimid, slno, Uhidno);

            return Newtonsoft.Json.JsonConvert.SerializeObject(maternitydt);

        }

        public string Checkpackageamount(string Uhidno)
        {

            DataTable maternitydt = null;
            maternitydt = _objMadicalScrutinyVM.Checkpackageamount(Uhidno);
            return Newtonsoft.Json.JsonConvert.SerializeObject(maternitydt);

        }

        public string CheckBenefitamount(string ClaimID)
        {

            DataTable benefitdt = null;
            benefitdt = _objMadicalScrutinyVM.CheckBenefitamount(ClaimID);

            return Newtonsoft.Json.JsonConvert.SerializeObject(benefitdt);

        }

        //SP3V-1432 END

        private void LoadBasicData(string ClaimID, string SlNo, string SID, string QMS = "", bool isFrmArchived = false, string QMSadmin = "")
        {
            ViewData["DMSApiURL"] = System.Web.Configuration.WebConfigurationManager.AppSettings["DMSApiURL"].ToString();
            ViewData["ClaimAIAuditURL"] = Convert.ToString(System.Web.Configuration.WebConfigurationManager.AppSettings["ClaimAIAuditURL"]);
            ViewData["AppLive"] = Session[SessionValue.AppLive];
            ViewData["AppURL"] = Session[SessionValue.AppURL];
            ViewData["ClaimID"] = ClaimID;
            ViewData["SlNo"] = SlNo;
            ViewData["ClaimStageID"] = SID;
            Session["ClaimStageID"] = SID;

            ViewData["QMS"] = QMS;
            ViewData["QMSadmin"] = QMSadmin;
            ViewData["EnableUCRSectionFields"] = _objMadicalScrutinyVM.ValidateUcrSectionAccess(Session[SessionValue.LoginUserID].ToString()) ? "1" : "0";
            DataTable IsCopay = _objMadicalScrutinyVM.GetIsClaimCopay(Session[SessionValue.LoginUserID].ToString());
            ViewData["AlertPaytmCorpClaims"] = _objMadicalScrutinyVM.CheckPaytmCorpClaimRecord(ClaimID, SlNo) ? "1" : "0";
            ViewData["IsCopay"] = IsCopay.Rows[0][0].ToString();
            ViewData["isFrmArchived"] = isFrmArchived.ToString();
            string ReferToInsurerIssueIds = Convert.ToString(ConfigurationManager.AppSettings["OnAuditReferToInsurer"]);
            ViewData["ReferToInsurerIssueIds"] = ReferToInsurerIssueIds;
            DataTable dtBasicData = _objMadicalScrutinyVM.ClaimMedicalScrutiny_LoadVM(Convert.ToInt64(ClaimID), Convert.ToInt16(SlNo), isFrmArchived);
            DataTable dtClaimsCoverageData = _objMadicalScrutinyVM.ClaimCoverages_LoadVM(Convert.ToInt64(ClaimID), Convert.ToInt16(SlNo), isFrmArchived);
            DataTable dtbhimavalidationsData = _objMadicalScrutinyVM.BhimaValidation_DataVM(Convert.ToInt64(ClaimID), Convert.ToInt16(SlNo));
            DataTable dtRadio1data = _objMadicalScrutinyVM.CheckBoxDetails_DataVM(Convert.ToInt64(ClaimID));
            DataTable dtRadio2data = _objMadicalScrutinyVM.CheckBoxDetails1_DataVM(Convert.ToInt64(ClaimID));
            DataTable dtRadio3data = _objMadicalScrutinyVM.CheckBoxDetails2_DataVM(Convert.ToInt64(ClaimID));
            DataTable dtRadio4data = _objMadicalScrutinyVM.CheckBoxDetails3_DataVM(Convert.ToInt64(ClaimID));

            if (dtbhimavalidationsData.Rows.Count > 0)
            {
                ViewData["dtbhimavalidationsData"] = dtbhimavalidationsData.Rows[0]["TotalCount"];
            }
            ViewData["dtRadio1data"] = dtRadio1data.Rows[0]["Radio1"];
            ViewData["dtRadio2data"] = dtRadio2data.Rows[0]["Radio2"];
            ViewData["dtRadio3data"] = dtRadio3data.Rows[0]["Radio3"];
            ViewData["dtRadio4data"] = dtRadio4data.Rows[0]["Radio4"];
            //SP3V-1623
            DataTable DtIsMaternityCovered = _objMadicalScrutinyVM.IsMaternityCovered(Convert.ToInt64(ClaimID), Convert.ToInt16(SlNo));
            if (DtIsMaternityCovered.Rows.Count > 0)
            {
                ViewData["IsMaternityCovered"] = "0";
            }
            else
            {
                ViewData["IsMaternityCovered"] = "1";
            }
            //SP3V-1623
            if (dtBasicData.Rows.Count > 0)
            {
                if (dtBasicData.Rows[0]["IssueID"].ToString() == "30")
                {
                    DataTable GetIsAdjucationFromReminders = _objMadicalScrutinyVM.GetIsAdjucationFromReminders(ClaimID, SlNo);

                    ViewData["IsAdjucationFromReminders"] = GetIsAdjucationFromReminders.Rows[0]["final_count"].ToString();

                }
                else
                {
                    ViewData["IsAdjucationFromReminders"] = "0";
                }


                string SBIPolicyNo = ConfigurationManager.AppSettings["SBIPolicyNo"].ToString().Trim().TrimStart(',');
                string[] SBIPolicyNoList = SBIPolicyNo.Split(',');
                string IsSBIPolicy = dtBasicData.Rows[0]["PolicyNo"].ToString();
                if (SBIPolicyNoList.Contains(IsSBIPolicy))
                {
                    ViewData["SBIPolicyNoList"] = "1";
                }
                else
                {
                    ViewData["SBIPolicyNoList"] = "0";
                }

                DataTable DtCheckMultipleMaternity = _objMadicalScrutinyVM.maternitysearch("PROCESS", ClaimID, SlNo, dtBasicData.Rows[0]["uhidno"].ToString());
                if (DtCheckMultipleMaternity.Rows.Count > 0)
                {
                    ViewData["CheckMultipleMaternity"] = "0";
                }
                else
                {
                    ViewData["CheckMultipleMaternity"] = "1";
                }
            }
            else
            {
                ViewData["CheckMultipleMaternity"] = "0";
                ViewData["SBIPolicyNoList"] = "0";
            }
            ViewData["LastSlNo"] = _objMadicalScrutinyVM.GetLastExtensionNumber(Convert.ToInt64(ClaimID));
            ViewData["dtClaimsCoverageData"] = JsonConvert.SerializeObject(dtClaimsCoverageData);
            if (dtBasicData.Rows.Count > 0)
            {
                var ISPNIDB = dtBasicData.Rows[0]["IsPolicyNIDB"];
                ViewData["IsPolicyNIDBforAudit"] = ISPNIDB;
                //Added by Rajesh Yerramsetti
                // Rajesh21_03_2013
                ViewData["IsAutomationClaim"] = dtBasicData.Rows[0]["IsAutomationClaim"];
                ViewData["isautoCashlessclaims"] = dtBasicData.Rows[0]["isautoCashlessclaims"];
                ViewData["ReviwedRemarks"] = dtBasicData.Rows[0]["ReviwedRemarks"];
                ViewData["isclaimwaitingperiod"] = "";
                ViewData["isAlimentnotcovered"] = "";
                ViewData["isDeductables"] = "";
                if (dtBasicData.Rows[0]["actIsAutomationClaim"].ToString() == "5" || dtBasicData.Rows[0]["actIsAutomationClaim"].ToString() == "6")
                    ViewData["IsAutomationClaimname"] = "SAA";
                else if (dtBasicData.Rows[0]["actIsAutomationClaim"].ToString() == "8" || dtBasicData.Rows[0]["actIsAutomationClaim"].ToString() == "9")
                    ViewData["IsAutomationClaimname"] = "PAA";

                if (Convert.ToInt32(dtBasicData.Rows[0]["IssueID"]) == 30 && Convert.ToString(dtBasicData.Rows[0]["PolicyType"]) == "4")
                {
                    DataTable dtexclusions = _objMadicalScrutinyVM.AckoLevelMemberExculsions(Convert.ToInt64(ClaimID), Convert.ToInt64(dtBasicData.Rows[0]["MemberpolicyID"]), Convert.ToByte(SlNo), 2);

                    if (dtexclusions.Rows.Count > 0)
                    {
                        bool isclaimwaitingperiod = dtexclusions.AsEnumerable().Any(row => (row.Field<int?>("BPConditionID") == 3) && row.Field<bool?>("iscovered") == true);
                        bool isDeductables = dtexclusions.AsEnumerable().Any(row => row.Field<int?>("BPConditionID") == 37 && row.Field<bool?>("iscovered") == true);
                        bool isPermanentExclusion = dtexclusions.AsEnumerable().Any(row => row.Field<int?>("BPConditionID") == 4 && row.Field<bool?>("iscovered") == true);
                        bool isPEDExclusion = dtexclusions.AsEnumerable().Any(row => row.Field<int?>("BPConditionID") == 2 && row.Field<bool?>("iscovered") == true);
                        if (isclaimwaitingperiod)
                        {
                            ViewData["isclaimwaitingperiod"] = "True";
                        }
                        else
                        {
                            ViewData["isclaimwaitingperiod"] = "";
                        }
                        if (isDeductables)
                        {
                            ViewData["isDeductables"] = "True";
                        }
                        else
                        {
                            ViewData["isDeductables"] = "";
                        }
                        if (isPermanentExclusion)
                        {
                            ViewData["isPermanentExclusion"] = "True";
                        }
                        else
                        {
                            ViewData["isPermanentExclusion"] = "";
                        }
                        if (isPEDExclusion)
                        {
                            ViewData["isPEDExclusion"] = "True";
                        }
                        else
                        {
                            ViewData["isPEDExclusion"] = "";
                        }
                    }
                }

                if (Convert.ToInt32(dtBasicData.Rows[0]["IssueID"]) > 0)
                {
                    DataTable MSTCoverages = _objMadicalScrutinyVM.GetMst_Coverages(Convert.ToInt32(dtBasicData.Rows[0]["IssueID"]), isFrmArchived);
                    ViewData["MSTCoverages"] = JsonConvert.SerializeObject(MSTCoverages);
                }
                if (dtBasicData.Rows.Count > 0 && dtBasicData.Rows[0]["StageID"].ToString() == "27")
                    ViewData["ClaimStageName"] = "Settled";
                else
                    ViewData["ClaimStageName"] = _objClaimsVM.GetClaimInternalStageName(Convert.ToInt32(SID), isFrmArchived);
                ViewData["BasicData"] = JsonConvert.SerializeObject(dtBasicData);
                ViewData["isVIP"] = dtBasicData.Rows[0]["isVIP"];
                ViewData["isSuspicious"] = dtBasicData.Rows[0]["isSuspicious"];
                ViewData["isNeftBounced"] = dtBasicData.Rows[0]["ISNeftBounced"];
                ViewData["LegalFlag"] = dtBasicData.Rows[0]["LegalFlag"];
                ViewData["OutofPlanPeriod"] = dtBasicData.Rows[0]["IsWithinpolicy"];
                //Enhancements On Suspicious & Legal Flag On Policy & Agent- TAGIC (SP-1381)
                ViewData["IsAnentSuspicious"] = dtBasicData.Rows[0]["IsAnentSuspicious"];
                //End Of Enhancements On Suspicious & Legal Flag On Policy & Agent- TAGIC (SP-1381)
                DataTable dtServiceData = _objMadicalScrutinyVM.ClaimServiceDetails_VM(Convert.ToInt64(ClaimID), Convert.ToInt16(SlNo), isFrmArchived);
                ViewData["ServiceData"] = JsonConvert.SerializeObject(dtServiceData);
                //task-SP 1538
                DataTable dtfamsuminsured = _objMadicalScrutinyVM.FamilySuminuredretrieve(Convert.ToInt64(ClaimID), Convert.ToInt16(SlNo), isFrmArchived);
                ViewData["dtfamsuminsured"] = JsonConvert.SerializeObject(dtfamsuminsured);
                ViewData["isAPICall"] = _objMadicalScrutinyVM.IsReferedToInsuerViaAPI(Convert.ToInt64(ClaimID), Convert.ToInt16(SlNo));
                // end task-SP 1538

                ViewData["score"] = dtBasicData.Rows[0]["score"]; // added by prasad
                ViewData["ReceivedMode_P23"] = dtBasicData.Rows[0]["ReceivedMode_P23"]; // SP3V-2383

                //SP3V-994 Leena 
                ViewData["IsSuspiciousPolicy"] = dtBasicData.Rows[0]["IsSuspiciousPolicy"];
                ViewData["IsAckoSuspiciousPolicy"] = dtBasicData.Rows[0]["IsAckoSuspiciousPolicy"];
                ViewData["Issueid"] = dtBasicData.Rows[0]["IssueID"];
                //End SP3V-994 Leena               

                //SP3V-3783 Leena
                ViewData["ProviderTempMOU"] = dtBasicData.Rows[0]["TempMOU"];
                //End SP3V-3783

                //SP3V-2758 Leena
                ViewData["ClaimMOUID"] = dtBasicData.Rows[0]["MOUID"];
                string clearText = dtBasicData.Rows[0]["ProviderID"].ToString() + "|" + dtBasicData.Rows[0]["MOUID"].ToString();
                string encryptString = new MasterUtilsBL().Encrypt(clearText, Convert.ToString(ConfigurationManager.AppSettings["URLEncryptionKey"]));
                ViewData["Tariffkey"] = Url.Encode(encryptString);
                //End SP3V-2758

                ViewData["IsSingleLetterEnabled"] = string.IsNullOrEmpty(dtBasicData.Rows[0]["IsSingleLetterEnabled"].ToString()) ? "0" : dtBasicData.Rows[0]["IsSingleLetterEnabled"].ToString();
                ViewData["CoverageType"] = string.IsNullOrEmpty(dtBasicData.Rows[0]["CoverageTypeID_P21"].ToString()) ? "0" : dtBasicData.Rows[0]["CoverageTypeID_P21"].ToString();



                if (dtBasicData.Rows.Count > 0)
                {
                    ViewData["RequestedAccomodation"] = JsonConvert.SerializeObject(_objClaimsVM.GetRequestedAccomodationVM(Convert.ToInt64(dtBasicData.Rows[0]["ProviderID"].ToString()), isFrmArchived));

                    ViewData["CautiousFlagging"] = dtBasicData.Rows[0]["hospital_flagging"];

                    if (Convert.ToInt16(dtBasicData.Rows[0]["ClaimTypeID"].ToString()) == 1)
                        ViewData["ClaimType"] = "Cashless";
                    else if (Convert.ToInt16(dtBasicData.Rows[0]["ClaimTypeID"].ToString()) == 2)
                        ViewData["ClaimType"] = "Reimbursement";
                    else
                        ViewData["ClaimType"] = "PART PP-MR";

                    ViewData["RequestType"] = _objMadicalScrutinyVM.GetRequestTypeName(Convert.ToInt16(dtBasicData.Rows[0]["RequestTypeID"]));
                    if (((Convert.ToInt16(dtBasicData.Rows[0]["RequestTypeID"])) == 1 || (Convert.ToInt16(dtBasicData.Rows[0]["RequestTypeID"])) == 2) && (Convert.ToBoolean(dtBasicData.Rows[0]["IsFinal"])) == true)
                        ViewData["IsFinal"] = "[Final]";
                    else
                        ViewData["IsFinal"] = "";
                    //SP3V-3079 - Leena------------------------------------------------------------
                    ViewData["CntEnhanceFinalRequest"] = 0;
                    if (dtBasicData.Rows[0]["IsAutomationClaim"] != null && dtBasicData.Rows[0]["IsAutomationClaim"].ToString() != "")
                    {
                        if (Convert.ToInt16(dtBasicData.Rows[0]["IsAutomationClaim"]) > 0)
                        {
                            ViewData["CntEnhanceFinalRequest"] = _objMadicalScrutinyVM.GetClaimEnhanceFinalRequest(Convert.ToInt64(ClaimID), Convert.ToString(dtBasicData.Rows[0]["RequestTypeID"]));
                        }
                    }
                    //End SP3V-3079 - Leena-------------------------------------------------------------

                    //SP3V-3449 BIMA-STARK START
                    ViewData["ClaimDetailsId"] = dtBasicData.Rows[0]["ClaimDetailsID"];
                    //SP3V-3449 BIMA-STARK END

                    #region Code for Validation for ThresholdAmt/Tds amount
                    if (Convert.ToInt16(dtBasicData.Rows[0]["ClaimTypeID"].ToString()) == 1 && Convert.ToInt16(dtBasicData.Rows[0]["RequestTypeID"].ToString()) == 4)
                    {
                        DataTable GteTDSDetails = _objMadicalScrutinyVM.GetSanctionedAmount(Convert.ToInt64(ClaimID), Convert.ToInt64(dtBasicData.Rows[0]["ProviderID"]), Convert.ToInt64(dtBasicData.Rows[0]["IssueID"]), isFrmArchived);
                        ViewData["GteTDSDetails"] = Newtonsoft.Json.JsonConvert.SerializeObject(GteTDSDetails);
                    }
                    else
                    {
                        ViewData["GteTDSDetails"] = "";
                    }
                    #endregion
                    //Code added By B srinu

                    string MemberPolicyID = Convert.ToString(dtBasicData.Rows[0]["MemberpolicyID"]);
                    string SITypeID = Convert.ToString(dtBasicData.Rows[0]["SITypeID"]);
                    ViewData["BSIData"] = "";// new ClaimsController().Get_BalanceSumInsured(MemberPolicyID, SITypeID, ClaimID, SlNo);
                    ViewData["Gender"] = _objMadicalScrutinyVM.GetGender(Convert.ToInt64(MemberPolicyID));

                    // Added by Venkat Mandadi
                    ViewBag.IsValidProvider = JsonConvert.SerializeObject(_objMadicalScrutinyVM.IsProviderExcludedOrBlacklist(ClaimID, dtBasicData.Rows[0]["ClaimTypeID"].ToString(), dtBasicData.Rows[0]["dateofadmission"].ToString(), isFrmArchived));

                    ViewData["IsCodingDisable"] = (SID == "24") ? 1 : 0; //For Task (SP-1103)

                    //SP-1453(Refer To Insurer Alert While Rejecting A Preauth)
                    ViewData["ReferToInsurerCount"] = _objMadicalScrutinyVM.ReferToInsurerCount(Convert.ToInt64(ClaimID), Convert.ToInt16(SlNo), isFrmArchived);
                    //End of SP-1453(Refer To Insurer Alert While Rejecting A Preauth)

                    DataTable GSTdata = _objMadicalScrutinyVM.GetGstDetails(Convert.ToInt64(ClaimID), Convert.ToInt16(SlNo), isFrmArchived);
                    ViewData["GSTdata"] = Newtonsoft.Json.JsonConvert.SerializeObject(GSTdata);
                    ViewData["ProviderGSTIN"] = dtBasicData.Rows[0]["ProviderGSTIN"].ToString();
                    //SP3V-1809
                    ViewData["ProposedLineofTreatment"] = JsonConvert.SerializeObject(_objClaimsVM.ProposedLineofTreatmentRetrive());

                    if (Convert.ToInt32(dtBasicData.Rows[0]["IssueID"]) == 32 && Convert.ToInt16(dtBasicData.Rows[0]["RequestTypeID"]) == 1 && Convert.ToInt16(ViewData["ClaimStageID"]) == 5)
                    {
                        ViewData["StarHealthEmployeeMsg"] = "1";
                    }
                    else
                    {
                        ViewData["StarHealthEmployeeMsg"] = "0";
                    }

                    //SP3V-1058---------------------------------------------------------------------------------
                    DataSet dsClaimBill = _objClaimsVM.Get_ServiceBillingDetailsVM(Convert.ToInt64(ClaimID), Convert.ToInt16(SlNo), isFrmArchived);
                    if (dsClaimBill != null)
                    {
                        DataTable dtClaimBill = dsClaimBill.Tables[5];
                        if (dtClaimBill != null)
                        {
                            ViewData["PackageDiscountRemarks"] = dtClaimBill.Rows[0]["PackageDiscountRemarks"].ToString();
                            ViewData["ServiceDiscountRemarks"] = dtClaimBill.Rows[0]["ServiceDiscountRemarks"].ToString();
                            ViewData["PackagePercentage"] = dtClaimBill.Rows[0]["PackagePercentage"].ToString();
                            Session["ClaimDiscount"] = dtClaimBill;
                        }
                        else
                        {
                            ViewData["PackageDiscountRemarks"] = "";
                            ViewData["ServiceDiscountRemarks"] = "";
                            ViewData["PackagePercentage"] = 0;
                        }
                        ViewData["BillClaimTypeID"] = dtBasicData.Rows[0]["ClaimTypeID"].ToString();
                    }

                    //End SP3V-1058--------------------------------------------------------------------------------
                }
            }
            else
            {
                ViewData["RequestedAccomodation"] = "";
                ViewData["ClaimType"] = "";
                ViewData["BSIData"] = "";
            }

            DataTable _dtCIReqInfo = _objMadicalScrutinyVM.ClaimsInwardRequestInfoByClaimID(ClaimID, SlNo, isFrmArchived);
            if (_dtCIReqInfo.Rows.Count > 0)
            {
                ViewBag.ClaimsInwardReqId = _dtCIReqInfo.Rows[0]["ID"].ToString();
                ViewBag.IsClaimProcessedByCI = 1;
            }
            else
            {
                ViewBag.ClaimsInwardReqId = 0;
                ViewBag.IsClaimProcessedByCI = 0;
            }
            ViewData["LoginType"] = Session[SessionValue.LoginType].ToString();
        }

        [HttpPost]
        [Authorize]
        [ValidateInput(false)]
        public ActionResult Index(FormCollection form)
        {
            Int64 ClaimID = Convert.ToInt64(form["ClaimID"]);
            Int16 SlNo = Convert.ToInt16(form["ClaimSlNo"]);
            string QMS = form["hdnQMS"].ToString();
            string QMSadmin = Convert.ToString(form["hdnQMSAdmin"]);
            int BillingType = 0;
            Session["ClaimStageID"] = form["ClaimStageID"].ToString();
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    // Added for Spectra-iAI integration (SP3V-4924)
                    ViewData["LoginUserID"] = Session[SessionValue.LoginUserID];
                    ViewData["LoginUserName"] = Session[SessionValue.LoginUserName];
                    ViewData["UserRoleID"] = Session[SessionValue.UserRoleID];
                    ViewData["UserDepartmentID"] = Session[SessionValue.UserDepartmentID];
                    ViewData["UserRegionID"] = Session[SessionValue.UserRegionID];
                    ViewData["LoginType"] = Session[SessionValue.LoginType];
                    //ViewData["iAIRedirectionURL"] = Convert.ToString(ConfigurationManager.AppSettings["iAIRedirectionURL"]);
                    // End (SP3V-4924)

                    if (Request.Form["btnSumbitCoding"] != null)
                    {
                        DataTable Providerid = _objClaimsVM.GetProviderID(ClaimID);
                        DataTable IsGipsaPTE = _objClaimsVM.GetProviderGipsa(Convert.ToInt64(Providerid.Rows[0]["Providerid"].ToString()));
                        DataTable vDataTable = (DataTable)JsonConvert.DeserializeObject(form["hdnClaimsCodingDetails"], (typeof(DataTable)));

                        vDataTable.Columns.Remove("ICDName");
                        vDataTable.Columns.Remove("DiseaseCode");

                        string GIPSA = IsGipsaPTE.Rows[0]["IsGipsa"].ToString(); //Provider Level Gipsa Status
                        string ISGIPSA = vDataTable.Rows[0]["isGipsa"].ToString(); //TPA Level Gipsa Status
                        string PackageAmount = form["txtTotalServicesPackageAmount"];
                        string BillAmount = form["txtTotalServicesBillAmount"];
                        decimal hdnNMEAmount = Convert.ToDecimal(form["hdnNMEAmount"].ToString());

                        if (ISGIPSA == "1")
                        {
                            ISGIPSA = "True";
                        }
                        if (GIPSA == "1")
                        {
                            GIPSA = "True";
                        }
                        if (form["BillingType"] == "")
                        {
                            BillingType = (Convert.ToInt64(PackageAmount == "" ? "0" : PackageAmount) != 0 && Convert.ToInt64(BillAmount == "" ? "0" : BillAmount) != 0) ? 203 : (Convert.ToInt64(PackageAmount == "" ? "0" : PackageAmount) != 0) ? 201 : 202;
                        }
                        else
                        {
                            BillingType = Convert.ToInt32(form["BillingType"]);
                        }
                        if (vDataTable.Columns.Contains("PPNCode"))
                        {
                            vDataTable.Columns.Remove("PPNCode");
                            vDataTable.Columns.Remove("FHPLCode");
                            vDataTable.Columns.Remove("FHPLDesc");
                            vDataTable.Columns.Remove("PPNDescription");
                        }
                        if ((Convert.ToInt32(form["ClaimStageID"]) == 5 || Convert.ToInt32(form["ClaimStageID"]) == 24 || Convert.ToInt32(form["ClaimStageID"]) == 22 || Convert.ToInt32(form["ClaimStageID"]) == 28 && Convert.ToInt32(form["ClaimTypeID"]) == 1))
                        {
                            string vMessage = string.Empty;
                            if (BillingType == 201 && GIPSA == "True" && ISGIPSA == "True" && (form["InsuranceCompanyID"].ToString() == "5" || form["InsuranceCompanyID"].ToString() == "6" || form["InsuranceCompanyID"].ToString() == "7" || form["InsuranceCompanyID"].ToString() == "8"))
                            {
                                DataTable dtClaimsServiceDetails = (DataTable)JsonConvert.DeserializeObject(form["ServiceDetails"], (typeof(DataTable)));
                                DataTable dtClaimBillDetails = (DataTable)JsonConvert.DeserializeObject(form["BillDetails"], (typeof(DataTable)));
                                DataTable dtClaimDeductionDetails = (DataTable)JsonConvert.DeserializeObject(form["DecuctionsDetails"], (typeof(DataTable)));
                                DataTable TariffDiscuont = (DataTable)JsonConvert.DeserializeObject(form["ServiceTariffAndDiscount"], (typeof(DataTable))); //SP3V - 4017
                                DataTable Accomationdays = (DataTable)JsonConvert.DeserializeObject(form["AccomdationRoomdays"], (typeof(DataTable)));
                                DataSet ds = _objClaimsVM.Get_ServiceBillingDetailsVM(Convert.ToInt64(ClaimID), Convert.ToInt16(SlNo), false);
                                DataTable Servicedetails = ds.Tables["Table"];
                                int ServiceTypeID = Convert.ToInt32(form["ddlServiceType"]);
                                int ServiceSubTypeID = Convert.ToInt32(form["ddlServiceSubType"]);
                                int RoleID = Convert.ToInt32(form["RoleID"]);

                                string EligibleAmount = form["txtTotalServicesEligibleAmount"];
                                string BillafterDeductions = form["txtTotServicesAfterDedAmt"];
                                string MOUDiscount = "0";
                                string TotalTariffAmount = form["TatalSeriveTariffAmount"];
                                string TotalBPAmount = form["TatalSeriveBPAmount"];
                                int Roomdays = Convert.ToInt16(Accomationdays.Rows[0]["Roomdays"].ToString());
                                int ICUdays = Convert.ToInt16(Accomationdays.Rows[0]["ICUdays"].ToString());
                                int LOS = Convert.ToInt16(Accomationdays.Rows[0]["ExtimatedDays"].ToString());

                                string ServiceBillRemarks = form["txtServiceBills_Remarks"];
                                decimal prop_dedu_percentage = Convert.ToDecimal(form["hdnproportionateperc"].ToString());

                                bool prop_dedu_appl_flag = false;
                                if (form["prop_dedu_appl_flag"] == "on")
                                    prop_dedu_appl_flag = true;
                                string prop_dedu_remarks = form["prop_dedu_remarks"];
                                if (dtClaimsServiceDetails != null && Servicedetails != null)
                                {
                                    if (dtClaimsServiceDetails.Rows.Count > 0 && Servicedetails.Rows.Count > 0)
                                    {
                                        Int64 TFAmount = 0;
                                        dtClaimsServiceDetails.Columns.Add("Internalvalue", typeof(string));
                                        dtClaimsServiceDetails.Columns.Add("BillafterdeductAmt", typeof(string));
                                        dtClaimsServiceDetails.Columns.Add("FinalTariffAmt", typeof(string));
                                        dtClaimsServiceDetails.Columns.Add("BillafterDiscountAmt", typeof(string));
                                        for (int i = 0; i < dtClaimsServiceDetails.Rows.Count; i++)
                                        {
                                            var obj = Servicedetails.AsEnumerable().Where(b => b.Field<byte>("ID") == Convert.ToInt16(dtClaimsServiceDetails.Rows[i]["ServiceID"].ToString())).ToList();
                                            //DataRow row = dtClaimsServiceDetails.NewRow();

                                            // row["Internalvalue"]= obj[0].ItemArray[4].ToString();
                                            dtClaimsServiceDetails.Rows[i]["Internalvalue"] = obj[0].ItemArray[4].ToString() == "" ? "0" : obj[0].ItemArray[4].ToString();
                                            dtClaimsServiceDetails.Rows[i]["BillafterdeductAmt"] = Convert.ToInt64(dtClaimsServiceDetails.Rows[i]["BillAmount"].ToString()) - Convert.ToInt64(dtClaimsServiceDetails.Rows[i]["DeductionAmount"].ToString() == "" ? "0" : dtClaimsServiceDetails.Rows[i]["DeductionAmount"].ToString());
                                            dtClaimsServiceDetails.Rows[i]["BillafterDiscountAmt"] = Convert.ToInt64(dtClaimsServiceDetails.Rows[i]["BillafterdeductAmt"].ToString()) - Convert.ToInt64(dtClaimsServiceDetails.Rows[i]["DiscountAmount"].ToString() == "" ? "0" : dtClaimsServiceDetails.Rows[i]["DiscountAmount"].ToString()); TFAmount = Convert.ToInt64(dtClaimsServiceDetails.Rows[i]["TariffAmount"].ToString() == "" ? "0" : dtClaimsServiceDetails.Rows[i]["TariffAmount"].ToString());
                                            if (Convert.ToInt16(dtClaimsServiceDetails.Rows[i]["ServiceID"].ToString()) == 2 && TFAmount != 0)
                                            {
                                                dtClaimsServiceDetails.Rows[i]["FinalTariffAmt"] = TFAmount * ICUdays;
                                            }
                                            else if (Convert.ToInt16(dtClaimsServiceDetails.Rows[i]["ServiceID"].ToString()) == 3 && TFAmount != 0)
                                            {
                                                dtClaimsServiceDetails.Rows[i]["FinalTariffAmt"] = TFAmount * Roomdays; ;
                                            }
                                            else if (Convert.ToInt16(dtClaimsServiceDetails.Rows[i]["ServiceID"].ToString()) == 4 && TFAmount != 0)
                                            {
                                                dtClaimsServiceDetails.Rows[i]["FinalTariffAmt"] = TFAmount * Roomdays; ;
                                            }
                                            else
                                            {
                                                dtClaimsServiceDetails.Rows[i]["FinalTariffAmt"] = TFAmount;
                                            }
                                        }
                                    }
                                }
                                DataTable vDtServices = new DataTable();
                                DataRow vdtRow;
                                // string vMessage = string.Empty;
                                //SP3V-1058 Leena
                                DataTable dtDisc = (DataTable)(Session["ClaimDiscount"]);
                                //END SP3V-1058

                                //SP3V-4017 Leena
                                //DataTable dtTariffServiceDisc = (DataTable)JsonConvert.DeserializeObject(form["ServiceTariffAndDiscount"], (typeof(DataTable)));
                                var _tariffJson1 = (form["ServiceTariffAndDiscount"] ?? "").Trim();
                                var objResponse1 = string.IsNullOrEmpty(_tariffJson1) ? new List<servicediscountdetails>() : JsonConvert.DeserializeObject<List<servicediscountdetails>>(_tariffJson1);
                                DataTable dtTariffServiceDisc = ToDataTable(objResponse1 ?? new List<servicediscountdetails>());
                                //End SP3V-4017 Leena
                                if (dtClaimBillDetails == null || dtClaimBillDetails.Rows.Count == 0)
                                {
                                    EligibleAmount = PackageAmount;
                                    int id = _objClaimsVM.Save_PackageDetailsVM(ClaimID, SlNo, EligibleAmount, PackageAmount, Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]), ServiceBillRemarks, dtDisc, dtTariffServiceDisc, out vMessage); //Pass dtDisc SP3V-1058 SP3V-4111
                                    int id1 = _objMadicalScrutinyVM.Save_CodingDetails(ClaimID, SlNo, BillingType, vDataTable, Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]), out vMessage, hdnNMEAmount);
                                    ViewData["BillsResult"] = vMessage;
                                    if (vMessage == "")
                                        ViewData["BillsResult"] = "Package details saved successfully";
                                    else
                                        ViewData["BillsResult"] = vMessage;
                                }
                                else
                                {
                                    vDtServices = dtClaimsServiceDetails.Clone();
                                    string remarks = string.Empty;
                                    for (int i = 0; i < dtClaimsServiceDetails.Rows.Count; i++)
                                    {
                                        //string TariffAmountt = "0";
                                        remarks = string.Empty;
                                        vdtRow = vDtServices.NewRow();
                                        vdtRow["ServiceID"] = dtClaimsServiceDetails.Rows[i]["ServiceID"].ToString();
                                        vdtRow["BillAmount"] = dtClaimsServiceDetails.Rows[i]["BillAmount"].ToString();
                                        vdtRow["DeductionAmount"] = dtClaimsServiceDetails.Rows[i]["DeductionAmount"].ToString();
                                        Int64 EligibleAmountt = Convert.ToInt64(dtClaimsServiceDetails.Rows[i]["EligibleAmount"].ToString());
                                        Int64 DiscountAmount = Convert.ToInt64(dtClaimsServiceDetails.Rows[i]["DiscountAmount"].ToString());
                                        Int64 SanctionedAmount = Convert.ToInt64(dtClaimsServiceDetails.Rows[i]["SanctionedAmount"].ToString());
                                        if (dtClaimsServiceDetails.Rows[i]["BillRoomdays"].ToString() == "" || dtClaimsServiceDetails.Rows[i]["BillRoomdays"].ToString() == null)
                                            vdtRow["BillRoomdays"] = 0;
                                        else
                                            vdtRow["BillRoomdays"] = dtClaimsServiceDetails.Rows[i]["BillRoomdays"].ToString();
                                        decimal d = Convert.ToDecimal(dtClaimsServiceDetails.Rows[i]["Internalvalue"].ToString());
                                        Int64 InternalVal = Convert.ToInt64(d);
                                        Int64 BillafterdeductAmt = Convert.ToInt64(dtClaimsServiceDetails.Rows[i]["BillafterdeductAmt"].ToString());
                                        // Int64 TariffAmount = Convert.ToInt64(dtClaimsServiceDetails.Rows[i]["TariffAmount"].ToString());
                                        Int64 TariffAmount = Convert.ToInt64(dtClaimsServiceDetails.Rows[i]["FinalTariffAmt"].ToString());
                                        Int64 BillafterDiscountSAmt = Convert.ToInt64(dtClaimsServiceDetails.Rows[i]["BillafterDiscountAmt"].ToString());
                                        if (dtClaimsServiceDetails.Rows[i]["DiscountAmount"].ToString() == "" || dtClaimsServiceDetails.Rows[i]["DiscountAmount"].ToString() == null)
                                            vdtRow["DiscountAmount"] = 0;
                                        else
                                            vdtRow["DiscountAmount"] = 0;
                                        if (BillafterDiscountSAmt == EligibleAmountt)
                                        {
                                            Int64 Tempamt = 0;
                                            if (InternalVal != 0 && TariffAmount != 0)
                                            {

                                                if (TariffAmount > InternalVal)
                                                {
                                                    Tempamt = InternalVal;
                                                }
                                                else if (TariffAmount < InternalVal)
                                                {
                                                    Tempamt = TariffAmount;
                                                }
                                                else
                                                {
                                                    Tempamt = TariffAmount;
                                                }
                                                if (Tempamt >= BillafterdeductAmt)
                                                {
                                                    vdtRow["EligibleAmount"] = BillafterdeductAmt;
                                                    vdtRow["SanctionedAmount"] = BillafterdeductAmt;
                                                }
                                                else
                                                {
                                                    vdtRow["EligibleAmount"] = Tempamt;
                                                    vdtRow["SanctionedAmount"] = Tempamt;
                                                }
                                            }
                                            else if (InternalVal != 0)
                                            {
                                                if (BillafterdeductAmt >= InternalVal)
                                                {
                                                    vdtRow["EligibleAmount"] = InternalVal;
                                                    vdtRow["SanctionedAmount"] = InternalVal;
                                                }
                                                else
                                                {
                                                    vdtRow["EligibleAmount"] = BillafterdeductAmt;
                                                    vdtRow["SanctionedAmount"] = BillafterdeductAmt;
                                                }
                                            }
                                            else if (TariffAmount != 0)
                                            {
                                                if (BillafterdeductAmt >= TariffAmount)
                                                {
                                                    vdtRow["EligibleAmount"] = TariffAmount;
                                                    vdtRow["SanctionedAmount"] = TariffAmount;
                                                }
                                                else
                                                {
                                                    vdtRow["EligibleAmount"] = BillafterdeductAmt;
                                                    vdtRow["SanctionedAmount"] = BillafterdeductAmt;
                                                }
                                            }

                                            else
                                            {
                                                vdtRow["EligibleAmount"] = BillafterdeductAmt;
                                                vdtRow["SanctionedAmount"] = BillafterdeductAmt;
                                            }
                                        }
                                        else
                                        {
                                            if (BillafterdeductAmt != EligibleAmountt)
                                            {
                                                if (InternalVal != 0 && TariffAmount != 0)
                                                {
                                                    if (TariffAmount >= InternalVal)
                                                    {
                                                        vdtRow["EligibleAmount"] = InternalVal;
                                                        vdtRow["SanctionedAmount"] = InternalVal;
                                                    }
                                                    else
                                                    {
                                                        vdtRow["EligibleAmount"] = TariffAmount;
                                                        vdtRow["SanctionedAmount"] = TariffAmount;
                                                    }
                                                }
                                                else if (TariffAmount != 0)
                                                {
                                                    vdtRow["EligibleAmount"] = TariffAmount;
                                                    vdtRow["SanctionedAmount"] = TariffAmount;
                                                }
                                                else
                                                {
                                                    vdtRow["EligibleAmount"] = InternalVal;
                                                    vdtRow["SanctionedAmount"] = InternalVal;
                                                }
                                            }
                                        }
                                        vDtServices.Rows.Add(vdtRow);
                                    }
                                    vDtServices.Columns.Remove("TariffAmount");
                                    vDtServices.Columns.Remove("Internalvalue");
                                    vDtServices.Columns.Remove("BillafterdeductAmt");
                                    vDtServices.Columns.Remove("FinalTariffAmt");
                                    vDtServices.Columns.Remove("BillafterDiscountAmt");


                                    if (dtClaimDeductionDetails == null)
                                    {
                                        int id = _objClaimsVM.Save_ServiceBillingDetailsVM(ClaimID, SlNo, dtClaimBillDetails, null, vDtServices,
                                            ServiceTypeID, ServiceSubTypeID, RoleID, Convert.ToInt32(Session[Resources.SessionValue.RegionID]),
                                            Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]), BillAmount, EligibleAmount, BillafterDeductions, PackageAmount, MOUDiscount, ServiceBillRemarks, TotalTariffAmount, TotalBPAmount, dtDisc, dtTariffServiceDisc, prop_dedu_percentage, prop_dedu_appl_flag, prop_dedu_remarks, out vMessage); //Pass dtDisc SP3V-1058 SP3V-411 SP3V-4017
                                        int id1 = _objMadicalScrutinyVM.Save_CodingDetails(ClaimID, SlNo, BillingType, vDataTable,
                                                 Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]), out vMessage, hdnNMEAmount);
                                        ViewData["BillsResult"] = vMessage;
                                    }
                                    else
                                    {
                                        int id = _objClaimsVM.Save_ServiceBillingDetailsVM(ClaimID, SlNo, dtClaimBillDetails, dtClaimDeductionDetails.Columns.Count == 0 ? null : dtClaimDeductionDetails, vDtServices,
                                           ServiceTypeID, ServiceSubTypeID, RoleID, Convert.ToInt32(Session[Resources.SessionValue.RegionID]),
                                           Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]), BillAmount, EligibleAmount, BillafterDeductions, PackageAmount, MOUDiscount, ServiceBillRemarks, TotalTariffAmount, TotalBPAmount, dtDisc, dtTariffServiceDisc, prop_dedu_percentage, prop_dedu_appl_flag, prop_dedu_remarks, out vMessage); //Pass dtDisc SP3V-1058 SP3V-411 SP3V-4017
                                        int id1 = _objMadicalScrutinyVM.Save_CodingDetails(ClaimID, SlNo, BillingType, vDataTable,
                                                 Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]), out vMessage, hdnNMEAmount);
                                        ViewData["BillsResult"] = vMessage;
                                    }
                                }
                            }
                            else
                            {
                                string vMessagee = string.Empty;
                                int id1 = _objMadicalScrutinyVM.Save_CodingDetails(ClaimID, SlNo, BillingType, vDataTable,
                                Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]), out vMessage, hdnNMEAmount);
                                vMessagee = vMessage;
                                ViewData["BillsResult"] = vMessagee;

                            }
                        }

                        else
                        {
                            string vMessage = string.Empty;
                            int id1 = _objMadicalScrutinyVM.Save_CodingDetails(ClaimID, SlNo, BillingType, vDataTable,
                                Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]), out vMessage, hdnNMEAmount);
                            //vMessagee = vMessage;
                            ViewData["BillsResult"] = vMessage;
                        }
                        Int16 RequestTypeID = Convert.ToInt16(form["RequestTypeID"]);
                        string vMessagE = string.Empty;
                        DataTable IsBuffer = _objClaimsVM.IsBufferUtilized(ClaimID, SlNo); //added by Bhagyaraj for SP-1216
                        string IsBuffereenable = IsBuffer.Rows[0]["Buffercount"].ToString();
                        if (Convert.ToInt16(IsBuffereenable) == 1 && RequestTypeID == 4)
                        {
                            int di = _objMadicalScrutinyVM.Save_BufferDetails(ClaimID, SlNo, Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]), out vMessagE);
                        }
                    }
                    else if (Request.Form["btnSumbitBilling"] != null)
                    {
                        #region Billing

                        DataTable dtClaimsServiceDetails = (DataTable)JsonConvert.DeserializeObject(form["ServiceDetails"], (typeof(DataTable)));
                        DataTable dtClaimBillDetails = (DataTable)JsonConvert.DeserializeObject(form["BillDetails"], (typeof(DataTable)));
                        DataTable dtClaimDeductionDetails = (DataTable)JsonConvert.DeserializeObject(form["DecuctionsDetails"], (typeof(DataTable)));
                        if (dtClaimsServiceDetails != null && dtClaimsServiceDetails.Rows.Count > 0)
                        {
                            DataColumnCollection columns = dtClaimsServiceDetails.Columns;
                            if (columns.Contains("TariffAmount"))
                            {
                                dtClaimsServiceDetails.Columns.Remove("TariffAmount");
                            }
                            if (!columns.Contains("BillRoomdays"))
                                dtClaimsServiceDetails.Columns.Add("BillRoomdays");
                        }
                        int ServiceTypeID = Convert.ToInt32(form["ddlServiceType"]);
                        int ServiceSubTypeID = Convert.ToInt32(form["ddlServiceSubType"]);
                        int RoleID = Convert.ToInt32(form["RoleID"]);

                        string BillAmount = form["txtTotalServicesBillAmount"];
                        string EligibleAmount = form["txtTotalServicesEligibleAmount"];
                        string BillafterDeductions = form["txtTotServicesAfterDedAmt"];
                        // string PackageAmount = form["txtTotalServicesPackageAmount"];
                        string PackageAmount = form["txtTotalServicesPackageAmount"] == null ? "" : form["txtTotalServicesPackageAmount"].ToString();
                        string MOUDiscount = form["TotalServiceDiscounts"];
                        string TotalTariffAmount = form["TatalSeriveTariffAmount"] == "NaN" ? "0" : form["TatalSeriveTariffAmount"];
                        string TotalBPAmount = form["TatalSeriveBPAmount"];

                        string ServiceBillRemarks = form["txtServiceBills_Remarks"];
                        decimal prop_dedu_percentage = Convert.ToDecimal(form["hdnproportionateperc"].ToString());
                        bool prop_dedu_appl_flag = false;
                        if (form["prop_dedu_appl_flag"] == "on")
                            prop_dedu_appl_flag = true;
                        string prop_dedu_remarks = form["prop_dedu_remarks"];

                        DataTable vDtServices = new DataTable();
                        DataRow vdtRow;
                        string vMessage = string.Empty;

                        //SP3V-1058 Leena
                        DataTable dtDisc = (DataTable)(Session["ClaimDiscount"]);
                        //end SP3V-1058
                        //SP3V-4017 Leena
                        //DataTable dtTariffServiceDisc = (DataTable)JsonConvert.DeserializeObject(form["ServiceTariffAndDiscount"], (typeof(DataTable)));
                        var _tariffJson2 = (form["ServiceTariffAndDiscount"] ?? "").Trim();
                        var objResponse1 = string.IsNullOrEmpty(_tariffJson2) ? new List<servicediscountdetails>() : JsonConvert.DeserializeObject<List<servicediscountdetails>>(_tariffJson2);
                        DataTable dtTariffServiceDisc = ToDataTable(objResponse1 ?? new List<servicediscountdetails>());

                        //End SP3V-4017 Leena
                        if (dtClaimBillDetails == null || dtClaimBillDetails.Rows.Count == 0)
                        {
                            EligibleAmount = PackageAmount;

                            int id = _objClaimsVM.Save_PackageDetailsVM(ClaimID, SlNo, EligibleAmount, PackageAmount, Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]), ServiceBillRemarks, dtDisc, dtTariffServiceDisc, out vMessage); //Pass dtdisc SP3V-1058 sp3v-411 Leena
                            SaveGSTdetails(ClaimID, SlNo, 0, 0, 0, 0);
                            if (vMessage == "")
                                ViewData["BillsResult"] = "Package details saved successfully" + Environment.NewLine + "GST is not applicable on Package amount.";
                            else
                                ViewData["BillsResult"] = vMessage;
                        }
                        else
                        {
                            vDtServices = dtClaimsServiceDetails.Clone();

                            //string discount = string.Empty;
                            string remarks = string.Empty;
                            for (int i = 0; i < dtClaimsServiceDetails.Rows.Count; i++)
                            {
                                // = string.Empty;
                                remarks = string.Empty;

                                ////discount = "txtDiscount_" + dtClaimsServiceDetails.Rows[i]["ServiceID"].ToString() + "_" + ClaimID;
                                //remarks = "txtRemarks_" + dtClaimsServiceDetails.Rows[i]["ServiceID"].ToString() + "_" + ClaimID;

                                vdtRow = vDtServices.NewRow();
                                vdtRow["ServiceID"] = dtClaimsServiceDetails.Rows[i]["ServiceID"].ToString();
                                vdtRow["BillAmount"] = dtClaimsServiceDetails.Rows[i]["BillAmount"].ToString();
                                vdtRow["DeductionAmount"] = dtClaimsServiceDetails.Rows[i]["DeductionAmount"].ToString();

                                //if (form[discount] == "" || form[discount] == null)
                                //    vdtRow[3] = 0; // DiscountAmount
                                //else
                                //    vdtRow[3] = form["" + discount + ""]; // DiscountAmount

                                if (dtClaimsServiceDetails.Rows[i]["DiscountAmount"].ToString() == "" || dtClaimsServiceDetails.Rows[i]["DiscountAmount"].ToString() == null)
                                    vdtRow["DiscountAmount"] = 0;
                                else
                                    vdtRow["DiscountAmount"] = dtClaimsServiceDetails.Rows[i]["DiscountAmount"].ToString();

                                if (dtClaimsServiceDetails.Rows[i]["EligibleAmount"].ToString() == "" || dtClaimsServiceDetails.Rows[i]["EligibleAmount"].ToString() == null)
                                    vdtRow["EligibleAmount"] = 0;
                                else
                                    vdtRow["EligibleAmount"] = dtClaimsServiceDetails.Rows[i]["EligibleAmount"].ToString();

                                if (dtClaimsServiceDetails.Rows[i]["SanctionedAmount"].ToString() == "" || dtClaimsServiceDetails.Rows[i]["SanctionedAmount"].ToString() == null)
                                    vdtRow["SanctionedAmount"] = 0;
                                else
                                    vdtRow["SanctionedAmount"] = dtClaimsServiceDetails.Rows[i]["SanctionedAmount"].ToString();

                                if (dtClaimsServiceDetails.Rows[i]["BillRoomdays"].ToString() == "" || dtClaimsServiceDetails.Rows[i]["BillRoomdays"].ToString() == null)
                                    vdtRow["BillRoomdays"] = 0;
                                else
                                    vdtRow["BillRoomdays"] = dtClaimsServiceDetails.Rows[i]["BillRoomdays"].ToString();

                                if (dtClaimsServiceDetails.Rows[i]["BillAmount"].ToString() == "0" || dtClaimsServiceDetails.Rows[i]["BillAmount"] == null)
                                {
                                    vdtRow["DiscountAmount"] = 0;
                                    vdtRow["SanctionedAmount"] = 0;
                                    vdtRow["EligibleAmount"] = 0;
                                }
                                //vdtRow["AdditionalAmount"] = null;
                                //vdtRow["AdditionalAmtReasonIDs"] = 0;
                                //vdtRow["CoPayment"] = null;
                                //vdtRow["Remarks"] = form["" + remarks + ""];
                                vDtServices.Rows.Add(vdtRow);

                            }
                            //if (vDtServices.Columns.Count > 6)
                            //{
                            //    if (vDtServices.Columns["TariffAmount"].ToString() == "TariffAmount")
                            //    {
                            //        vDtServices.Columns.Remove("TariffAmount");
                            //    }
                            //}

                            if (dtClaimDeductionDetails == null)
                            {
                                int id = _objClaimsVM.Save_ServiceBillingDetailsVM(ClaimID, SlNo, dtClaimBillDetails, null, vDtServices,
                                    ServiceTypeID, ServiceSubTypeID, RoleID, Convert.ToInt32(Session[Resources.SessionValue.RegionID]),
                                    Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]), BillAmount, EligibleAmount, BillafterDeductions, PackageAmount, MOUDiscount, ServiceBillRemarks, TotalTariffAmount, TotalBPAmount, dtDisc, dtTariffServiceDisc, prop_dedu_percentage, prop_dedu_appl_flag, prop_dedu_remarks, out vMessage); //SP3V-1058 - SP3V-411 SP3V-4017
                            }
                            else
                            {
                                int id = _objClaimsVM.Save_ServiceBillingDetailsVM(ClaimID, SlNo, dtClaimBillDetails, dtClaimDeductionDetails.Columns.Count == 0 ? null : dtClaimDeductionDetails, vDtServices,
                                   ServiceTypeID, ServiceSubTypeID, RoleID, Convert.ToInt32(Session[Resources.SessionValue.RegionID]),
                                   Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]), BillAmount, EligibleAmount, BillafterDeductions, PackageAmount, MOUDiscount, ServiceBillRemarks, TotalTariffAmount, TotalBPAmount, dtDisc, dtTariffServiceDisc, prop_dedu_percentage, prop_dedu_appl_flag, prop_dedu_remarks, out vMessage); //SP3V-1058 - SP3V-411 SP3V-4017
                            }

                            if (vMessage == "" && prop_dedu_percentage > 0 && prop_dedu_appl_flag == false)
                                ViewData["BillsResult"] = "Bill details saved successfully and Proportionate deductions applied";
                            else if (vMessage == "")
                                ViewData["BillsResult"] = "Bill details saved successfully";
                            else
                                ViewData["BillsResult"] = vMessage;
                            if (form["gst_tab"] != null && form["gst_tab"] == "on")
                            {
                                decimal GST = Convert.ToDecimal(form["gst_123"] == null || form["gst_123"] == "" ? "0" : form["gst_123"].ToString());
                                decimal IGST = Convert.ToDecimal(form["igst_123"] == null || form["igst_123"] == "" ? "0" : form["igst_123"].ToString());
                                decimal CGST = Convert.ToDecimal(form["cgst_123"] == null || form["cgst_123"] == "" ? "0" : form["cgst_123"].ToString());
                                decimal SGST = Convert.ToDecimal(form["sgst_123"] == null || form["cgst_123"] == "" ? "0" : form["sgst_123"].ToString());
                                SaveGSTdetails(ClaimID, SlNo, GST, IGST, CGST, SGST);
                                ViewData["BillsResult"] = ViewData["BillsResult"] + Environment.NewLine + "GST details saved successfully";

                            }
                            else
                            {
                                SaveGSTdetails(ClaimID, SlNo, 0, 0, 0, 0);
                                ViewData["BillsResult"] = ViewData["BillsResult"] + Environment.NewLine + "You have selected the GST number as Not Available";

                            }
                        }

                        LoadBasicData(ClaimID.ToString(), SlNo.ToString(), Session["ClaimStageID"].ToString(), QMS.ToString(), false, QMSadmin);

                        return View();

                        //return RedirectToAction("Index", "Claims");
                        #endregion Billing



                    }

                    LoadBasicData(ClaimID.ToString(), SlNo.ToString(), Session["ClaimStageID"].ToString(), QMS.ToString(), false, QMSadmin);

                    return View();

                }
                else
                {
                    //return "ErrorCode#1";
                    ViewData["BillsResult"] = "Session has been expired.. please re-login.";
                    return View();
                }
            }
            catch (Exception ex)
            {
                if (Request.Form["btnSumbitCoding"] != null)
                    _objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutiny", "Save_CodingDetails", Session[Resources.SessionValue.LoginUserID].ToString());
                else if (Request.Form["btnSumbitBilling"] != null)
                    _objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutiny", "SaveServiceBillDetails", Session[Resources.SessionValue.LoginUserID].ToString());

                ViewData["BillsResult"] = ex.Message;
                //return RedirectToAction("Index");

                LoadBasicData(ClaimID.ToString(), SlNo.ToString(), Session["ClaimStageID"].ToString(), QMS.ToString(), false, QMSadmin);

                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));

                return View();
                //return RedirectToAction("Index", "Claims");
            }
        }

        [Authorize]
        public ActionResult ClaimsView(string ClaimID, string SlNo, string SID, bool IsFrmArchived = false)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    //if (_objCommon.IsValidRole(new int[] { 13, 14, 15, 16, 17, 18, 19, 32 }, (int[])Session["AllowedRoles"]))
                    //{
                    //Prasad Rage
                    ViewData["LoginUserID"] = Session[SessionValue.LoginUserID];

                    // Added for Spectra-iAI integration (SP3V-4924)
                    ViewData["LoginUserName"] = Session[SessionValue.LoginUserName];
                    ViewData["UserRoleID"] = Session[SessionValue.UserRoleID];
                    ViewData["UserDepartmentID"] = Session[SessionValue.UserDepartmentID];
                    ViewData["UserRegionID"] = Session[SessionValue.UserRegionID];
                    ViewData["LoginType"] = Session[SessionValue.LoginType];
                    //ViewData["iAIRedirectionURL"] = Convert.ToString(ConfigurationManager.AppSettings["iAIRedirectionURL"]);
                    // End (SP3V-4924)

                    //DMS Integration
                    ViewData["DMSApiURL"] = System.Web.Configuration.WebConfigurationManager.AppSettings["DMSApiURL"].ToString();
                    //DMS APIKey Authentication
                    string clientId = ConfigurationManager.AppSettings["ClientID"];
                    string DMSAPIKey = ConfigurationManager.AppSettings["DMSAPIKey"];
                    string clearText = clientId + "|" + DMSAPIKey;
                    ViewData["qString"] = new MasterUtilsBL().Encrypt(clearText, Convert.ToString(ConfigurationManager.AppSettings["URLEncryptionKey"]));
                    LoadBasicData(ClaimID, SlNo, SID, "", IsFrmArchived);
                    return View();
                    //}
                    //else
                    //{
                    //    return View("AccessDenied");
                    //}
                }
                else
                {
                    return RedirectToAction("MCareLogin", "Account");
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "Load", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return RedirectToAction("Error");
            }
        }


        /* Start Srividya Code*/
        [HttpGet]
        public string ClaimAudit_Retrieve(long ClaimID, int SlNo, bool isFrmArchived = false)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.ClaimAudit_Retrieve(ClaimID, SlNo, isFrmArchived));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "ClaimAudit_Retrieve", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }
        [HttpGet]
        public string ClaimPending_Reasons_Retrieve(long ClaimID, int SlNo, long ActionID, int StageID)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.ClaimPending_Reasons_Retrieve(ClaimID, SlNo, ActionID, StageID));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "ClaimAudit_Retrieve", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));

                ReturnError rtnObj = new ReturnError();
                rtnObj.ID = 1;
                rtnObj.Message = "Error while getting reasons.";
                return Newtonsoft.Json.JsonConvert.SerializeObject(rtnObj);

                //return ex.Message;
            }
        }
        [HttpGet]
        public string ClaimCommunication_Retrieve(long ClaimID, int SlNo, bool IsFrmArchived = false)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.ClaimCommunication_Retrieve(ClaimID, SlNo, IsFrmArchived));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "ClaimCommunication_Retrieve", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }

        }

        [HttpGet]
        public string ClaimHistoryCommunication_Retrieve(long ClaimID, int SlNo, bool IsFrmArchived)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.ClaimHistoryCommunication_Retrieve(ClaimID, SlNo, IsFrmArchived));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "ClaimCommunication_Retrieve", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }

        }
        //[HttpGet]
        //public string ClaimAttachment_Retrieve(long ClaimID)
        //{
        //    try
        //    {
        //        if (Session[SessionValue.UserRegionID] != null)
        //        {
        //            return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.ClaimAttachment_Retrieve(ClaimID));
        //        }
        //        else
        //        {
        //            //new CommonController().ErrorLog_Insert("Session Expired", "ClaimsController", "InsertPreauthRequest-Claim DashBoard");
        //            return "ErrorCode#1";
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.Fatal("MedicalScrutinyController - ClaimCommunication_Retrieve : " + ex.Message);
        //        throw;
        //    }

        //}

        public string ClaimAttachment_Retrieve(long ClaimID, bool IsFrmArchived = false)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    DataSet dataSet = _objMadicalScrutinyVM.ClaimAttachment_Retrieve(ClaimID, IsFrmArchived);
                    if (dataSet != null && dataSet.Tables.Count > 1)
                    {
                        if (dataSet.Tables[1] != null && dataSet.Tables[1].Rows.Count > 0)
                        {
                            for (int i = 0; i < dataSet.Tables[1].Rows.Count; i++)
                            {
                                //string webShareUrl = ConfigurationManager.AppSettings["WebShareURL"].ToString();
                                string directoryName = ConfigurationManager.AppSettings["DMSDirectoryName"].ToString();
                                string path = dataSet.Tables[1].Rows[i]["FilePath"].ToString() + dataSet.Tables[1].Rows[i]["SystemFileName"].ToString();
                                string buckeName = path.Contains("/FAXServer/") ? ConfigurationManager.AppSettings["S3FaxserverBucketName"].ToString() : ConfigurationManager.AppSettings["S3SpectraBucketName"].ToString();

                                if (directoryName.Split(',').Length > 0)
                                {
                                    foreach (var item in directoryName.Split(','))
                                    {
                                        path = path.Replace(item, "");
                                    }
                                }

                                ProviderController providerController = new ProviderController();
                                string presingedUrl = providerController.GeneratePresignedURL(buckeName, path, 15);

                                if (!string.IsNullOrEmpty(presingedUrl))
                                {
                                    dataSet.Tables[1].Rows[i]["FilePath"] = presingedUrl.Replace("https:", "");
                                    dataSet.Tables[1].Rows[i]["SystemFileName"] = "";
                                }
                            }
                        }
                    }
                    return Newtonsoft.Json.JsonConvert.SerializeObject(dataSet);
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "ClaimAttachment_Retrieve", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }

        }

        public bool WebURLExists(string url)
        {
            System.Net.WebRequest webRequest = System.Net.WebRequest.Create(url);
            webRequest.Method = "HEAD";
            try
            {
                using (System.Net.HttpWebResponse response = (System.Net.HttpWebResponse)webRequest.GetResponse())
                {
                    if (response.StatusCode.ToString() == "OK")
                    {
                        return true;
                    }
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        public string ClaimRules_Trigger(int IssueID, decimal ApprovedAmt, Int64 ClaimID, Int64 MemberPolicyID, Int64 MainmemberPolicyID, int StageID)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.ClaimRules_Trigger(IssueID, ApprovedAmt, ClaimID, MemberPolicyID, MainmemberPolicyID, StageID));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "ClaimRules_Trigger", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        public string Claims_RuleEngine(Int64 ClaimID, Int64 MemberPolicyID, byte Slno)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.Claims_RuleEngine(ClaimID, MemberPolicyID, Slno));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "Claims_RuleEngine", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }
        [HttpGet]
        [Authorize]
        public string ClaimRules_Retrieve(long ClaimID, int SlNo)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.ClaimRules_Retrieve(ClaimID, SlNo));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "ClaimRules_Retrieve", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }


        //public string ClaimRules_Insert(string ClaimDetails, string Rules, Decimal BillAmount, Decimal EligibleBillAmount, Decimal EligibleAmount, Decimal SanctionedAmount,
        //     Decimal TDSAmount, Decimal NetAmount, Decimal MOUDiscount, Decimal DiscountByHospital, Decimal Deductible, Decimal PaidByPatient, Decimal ExcessPaidByPatient,
        //     Decimal CoPayment, string ClaimUtilization)
        public string ClaimRules_Insert(string ClaimDetails, string Rules, Decimal DiscountByHospital, Decimal EligibleAmount, Decimal Deductible, Decimal CoPayment,
            Decimal NetEligibleAmount, Decimal Excess_SI, Decimal Excess_Preauth, Decimal ExcessPaidByPatient, Decimal AdmissibleAmount, Decimal EligiblePayableAmount,
            Decimal NegotiatedAmount, Decimal GrossAmount, Decimal TDSAmount, Decimal NetAmount, Decimal PaidByPatient, Decimal BufferUtilized, string Copayhtml, string ClaimUtilization, string DoctorNotes, string AdditionalNotes, bool NottoDeductFromHospital, Decimal EarlyPaymentDiscountAmount, bool SkipScrutiny, Decimal PremiumDeducted,
            string QMSID, string QMSAdminID, Decimal Modularamount, Decimal Patienttobepaid, bool Adj_IsFinal, string Isrefertocrm, bool SkipAudit, Decimal? PMTNegotiatedDiscount)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {

                    string QMS = string.Empty;
                    QMS = QMSID;
                    string QMSadmin = string.Empty;
                    QMSadmin = QMSAdminID;

                    string msg;
                    //DataTable lst = new DataTable("Something1");
                    //lst.TableName = "Something1";
                    DataTable rules = null;
                    if (Rules != "" && Rules != "[]" && Rules != null)
                        rules = (DataTable)JsonConvert.DeserializeObject(Rules, (typeof(DataTable)));

                    //  DataTable lst1 = (DataTable)JsonConvert.DeserializeObject(ClaimDetails, (typeof(DataTable)));
                    Newtonsoft.Json.Linq.JObject JObject = Newtonsoft.Json.Linq.JObject.Parse(ClaimDetails);
                    ClaimActionItems objActionIteams = new ClaimActionItems();
                    objActionIteams.ClaimID = Convert.ToInt64(JObject["ClaimID"]);
                    objActionIteams.Slno = Convert.ToInt16(JObject["Slno"]);
                    objActionIteams.ClaimTypeID = Convert.ToInt16(JObject["ClaimTypeID"]);
                    objActionIteams.RequestTypeID = Convert.ToInt16(JObject["RequestTypeID"]);
                    objActionIteams.ServiceTypeID = Convert.ToInt16(JObject["ServiceTypeID"]);
                    objActionIteams.ServiceSubTypeID = Convert.ToInt16(JObject["ServiceSubTypeID"]);
                    objActionIteams.ClaimStageID = Convert.ToInt32(JObject["ClaimStageID"]);
                    objActionIteams.RoleID = Convert.ToInt32(JObject["RoleID"]);
                    objActionIteams.RegionID = Convert.ToInt32(Session[Resources.SessionValue.RegionID]);
                    objActionIteams.ClaimedAmount = Convert.ToDecimal(JObject["ClaimedAmount"]);
                    objActionIteams.ClosedBy = Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]);

                    //DataTable dtUtilization = new DataTable("Something");
                    //dtUtilization.TableName = "Something";
                    DataTable dtUtilization = null;
                    if (ClaimUtilization != "" && ClaimUtilization != "[]" && ClaimUtilization != null)
                        dtUtilization = (DataTable)JsonConvert.DeserializeObject(ClaimUtilization, (typeof(DataTable)));
                    //DataTable dtUtilization = (DataTable)JsonConvert.DeserializeObject(ClaimUtilization, (typeof(DataTable)));

                    _objMadicalScrutinyVM.ClaimRules_Insert(objActionIteams, rules, DiscountByHospital, EligibleAmount, Deductible, CoPayment, NetEligibleAmount, Excess_SI, Excess_Preauth, ExcessPaidByPatient, NottoDeductFromHospital, AdmissibleAmount, EligiblePayableAmount, NegotiatedAmount, GrossAmount, TDSAmount, NetAmount, PaidByPatient, Copayhtml, dtUtilization, DoctorNotes, AdditionalNotes, BufferUtilized, EarlyPaymentDiscountAmount, SkipScrutiny, PremiumDeducted, Modularamount, Patienttobepaid, Adj_IsFinal, Isrefertocrm, SkipAudit, PMTNegotiatedDiscount, out msg);
                    //_objMadicalScrutinyVM.ClaimRules_Insert(objActionIteams, rules,

                    //Claim Lock Release Code By Srinu B
                    new DefaultCacheProvider().Invalidate(Convert.ToString(JObject["ClaimID"]));
                    Qmsv2CMController qms = new Qmsv2CMController();
                    qms.UpdateClaimStatus("UPDATESTATUS", "", "", "", "", QMS, "5", Session["UserRegionID"].ToString());
                    return Newtonsoft.Json.JsonConvert.SerializeObject(msg);

                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //    _objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "ClaimRules_Insert", Session[Resources.SessionValue.LoginUserID].ToString());
                //    throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        [HttpGet]
        public string AllRejectionReasons_Retrieve(long ClaimID, int SlNo)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.AllRejectionReasons_Retrieve(ClaimID, SlNo));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "AllRejectionReasons_Retrieve", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }


        public string Adjudication_Actions_Insert(string ClaimDetails, string QMSID, string QMSAdminID, string ClaimRejections, string ddlInvestigationReasons = ""
            , string ddlGroundofRepudiation = "", string ddlRecommendation = "", string ddlClaimantReason = "", string ddlHospitalReason = ""
            , string txtSuspect_Fraudster_Name = "", string ddlSuspect_Fraudster_Proof_ID = "", string txtSuspect_Fraudster_ID_Proof_Number = "", string txtfieldofcname = "", string RTI_deductible = "0", bool Is_final = false)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    string msg;
                    //  DataTable lst1 = (DataTable)JsonConvert.DeserializeObject(ClaimDetails, (typeof(DataTable)));
                    Newtonsoft.Json.Linq.JObject JObject = Newtonsoft.Json.Linq.JObject.Parse(ClaimDetails);
                    ClaimActionItems objActionIteams = new ClaimActionItems();
                    objActionIteams.ClaimID = Convert.ToInt64(JObject["ClaimID"]);
                    objActionIteams.Slno = Convert.ToInt16(JObject["Slno"]);
                    objActionIteams.ClaimTypeID = Convert.ToInt16(JObject["ClaimTypeID"]);
                    objActionIteams.RequestTypeID = Convert.ToInt16(JObject["RequestTypeID"]);
                    objActionIteams.ServiceTypeID = Convert.ToInt16(JObject["ServiceTypeID"]);
                    objActionIteams.ServiceSubTypeID = Convert.ToInt16(JObject["ServiceSubTypeID"]);
                    objActionIteams.ClaimStageID = Convert.ToInt32(JObject["ClaimStageID"]);
                    objActionIteams.RoleID = Convert.ToInt32(JObject["RoleID"]);
                    objActionIteams.RegionID = Convert.ToInt32(Session[Resources.SessionValue.RegionID]);
                    objActionIteams.ClaimedAmount = Convert.ToDecimal(JObject["ClaimedAmount"]);
                    objActionIteams.ReasonIDs_P = Convert.ToString(JObject["ReasonIDs_P"]);
                    objActionIteams.Remarks = Convert.ToString(JObject["Remarks"]);
                    objActionIteams.ClosedBy = Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]);
                    int IssueId = Convert.ToInt16(JObject["issueID"]);
                    string InsPerson = Convert.ToString(JObject["InsPerson"]);
                    DataTable dtClaimRejections = GetClaimRejectionTableStructure(ClaimRejections);
                    string officer = txtfieldofcname;
                    #region duplicate code
                    //_objMadicalScrutinyVM.Adjudication_Actions_Insert(objActionIteams, out msg);



                    //SP3V-251 - Requirement to create only an SMS template for Kotak in Claim Investigation stage
                    #endregion
                    DataSet dsResult = new DataSet();// null;
                    msg = "";

                    // DataTable is_refer_to_insurer = _objMadicalScrutinyVM.Check_Insurer_Refer_to_insurer(Convert.ToInt64(objActionIteams.ClaimID), Convert.ToInt32(objActionIteams.Slno), 1);
                    if (objActionIteams.ReasonIDs_P == "221" || objActionIteams.ReasonIDs_P == "225")
                    {
                        _objMadicalScrutinyVM.ClaimRejection_Validate(Convert.ToInt64(objActionIteams.ClaimID), Convert.ToInt32(objActionIteams.Slno), dtClaimRejections, Convert.ToDecimal(RTI_deductible), Convert.ToDecimal(0), Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]), Convert.ToInt32(Session[Resources.SessionValue.RegionID]), Convert.ToInt32(objActionIteams.ReasonIDs_P), out msg);
                        if (!msg.Contains("Saved Successfully"))
                        {
                            return msg;
                        }
                        // insertClaimRejectedreason(Convert.ToInt64(JObject["ClaimID"]), Convert.ToInt16(JObject["Slno"]),23);
                    }
                    dsResult = _objMadicalScrutinyVM.Adjudication_Actions_Insert(objActionIteams, dtClaimRejections, out msg, ddlInvestigationReasons
                        , ddlGroundofRepudiation, ddlRecommendation, ddlClaimantReason, ddlHospitalReason, txtSuspect_Fraudster_Name
                        , ddlSuspect_Fraudster_Proof_ID, txtSuspect_Fraudster_ID_Proof_Number, officer, Is_final);

                    string QMS = string.Empty;
                    QMS = QMSID;
                    string QMSadmin = string.Empty;
                    QMSadmin = QMSAdminID;

                    Qmsv2CMController qms = new Qmsv2CMController();
                    qms.UpdateClaimStatus("UPDATESTATUS", "", "", "", "", QMS, "5", Session["UserRegionID"].ToString());
                    if (dsResult.Tables.Count != 0)
                    {
                        if (dsResult.Tables.Count != 0)
                        {
                            if (dsResult.Tables[0].Rows.Count > 0 && dsResult.Tables[1].Rows.Count > 0 && Convert.ToInt32(dsResult.Tables[1].Rows[0]["IssueId"].ToString()) == 20)//Kotak SMS 
                            {
                                _objCommon.CommunicationInsert_Common(ref dsResult, Convert.ToInt64(JObject["ClaimID"]), Convert.ToInt32(JObject["Slno"]), Convert.ToInt64(dsResult.Tables[1].Rows[0]["MemberPolicyID"].ToString()), 0, 0, 0, 0, 0, Convert.ToInt32(20), 18, "MedicalScrutinyController", Convert.ToInt32(Session[SessionValue.UserRegionID]), 0, 0);

                            }
                        }
                    }
                    var insurerresobj = new refertoinsresponse();
                    if (IssueId == 10 && objActionIteams.ClaimStageID == 17 && (objActionIteams.RequestTypeID == 1 || objActionIteams.RequestTypeID == 2 || objActionIteams.RequestTypeID == 3)
                        && msg.Contains("Saved Successfully"))
                    {
                        try
                        {

                            string token = Convert.ToString(ConfigurationManager.AppSettings["ITGItoken"]);// "RmhwbEtleTpwc2RmZyRqa2wzNDU=";
                            var client = new HttpClient();
                            string apiUrl = Convert.ToString(ConfigurationManager.AppSettings["ITGIAPIurl"]); //"https://uat-spectra.fhpl.net/api/ITIC/SpectraAuthPush";
                            refertoinsurerAPIRequest docReq = new refertoinsurerAPIRequest();
                            docReq.ClaimID = Convert.ToInt64(objActionIteams.ClaimID);
                            docReq.Slno = Convert.ToInt32(objActionIteams.Slno);
                            var jsonDoc = JsonConvert.SerializeObject(docReq).ToString();
                            var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
                            request.Headers.Add("Authorization", "Basic " + token);
                            var content = new StringContent(jsonDoc.ToString(), null, "application/json");
                            request.Content = content;
                            var response = client.SendAsync(request);
                            var insurerresponse = response.Result.Content.ReadAsStringAsync().Result;
                            insurerresobj = JsonConvert.DeserializeObject<refertoinsresponse>(insurerresponse);
                            msg = "Status Code" + " " + insurerresobj.StatusCode + " " + insurerresobj.Message;
                        }
                        catch (Exception ex)
                        {
                            msg = "Status Code" + " " + 100 + " " + "Internal server, please contact admin";
                        }
                    }
                    //SP3V-251 -End of  Requirement to create only an SMS template for Kotak in Claim Investigation stage
                    DataSet dtresult1 = new DataSet();
                    string msg1 = "";
                    DataTable Data_refer_to_insurer = _objMadicalScrutinyVM.Check_Insurer_Refer_to_insurer(Convert.ToInt64(objActionIteams.ClaimID), Convert.ToInt32(objActionIteams.Slno), 2);
                    if (Data_refer_to_insurer.Rows.Count > 0 && msg.Contains("Saved Successfully"))
                    {
                        if (Data_refer_to_insurer.Rows[0]["Isrefertoinsurer"].ToString() == "True" && objActionIteams.ReasonIDs_P == "224" && Convert.ToInt32(objActionIteams.ClaimStageID) == 14 && Data_refer_to_insurer.Rows[0]["RTI_reasonID"].ToString() == "220")
                        {
                            dtresult1 = _objMadicalScrutinyVM.USP_ClaimAudit_Insert_Refer_to_Insurer(objActionIteams, out msg1);
                            if (dtresult1.Tables.Count != 0)
                            {
                                if (Convert.ToInt16(JObject["RequestTypeID"]) == 1 || Convert.ToInt16(JObject["RequestTypeID"]) == 2 || Convert.ToInt16(JObject["RequestTypeID"]) == 3 && VVflag == 0 && dtresult1.Tables[1].Rows.Count > 0)
                                    msg1 = msg1 + " ; " + Save_ePreauthDetails(24, Convert.ToInt64(JObject["ClaimID"]), Convert.ToInt32(JObject["Slno"]));
                                if (dtresult1.Tables[0].Rows.Count > 0 && dtresult1.Tables[1].Rows.Count > 0)//&& dsResult.Tables[2].Rows.Count > 0)
                                {
                                    if (Convert.ToInt64(JObject["ClaimID"]) != 0)
                                        _objCommon.CommunicationInsert_Common(ref dtresult1, Convert.ToInt64(JObject["ClaimID"]), Convert.ToInt32(JObject["Slno"]), Convert.ToInt64(Data_refer_to_insurer.Rows[0]["mainMemberID"].ToString()),
                                            Convert.ToInt64(Data_refer_to_insurer.Rows[0]["policyID"].ToString()), Convert.ToInt64(Data_refer_to_insurer.Rows[0]["providerID"].ToString()), Convert.ToInt32(Data_refer_to_insurer.Rows[0]["BrokerID"].ToString() == "" ? "0" : Data_refer_to_insurer.Rows[0]["BrokerID"].ToString()), Convert.ToInt64(Data_refer_to_insurer.Rows[0]["CorporateID"].ToString()),
                                            Convert.ToInt64(Data_refer_to_insurer.Rows[0]["PayerID"].ToString()), IssueId, 24, "MedicalScrutinyController", Convert.ToInt32(Session[SessionValue.UserRegionID]), 0, Convert.ToInt32(Data_refer_to_insurer.Rows[0]["AgentID"].ToString() == "" ? "0" : Data_refer_to_insurer.Rows[0]["AgentID"].ToString()));
                                }
                            }
                        }
                        else if (Data_refer_to_insurer.Rows[0]["Isrefertoinsurer"].ToString() == "True" && objActionIteams.ReasonIDs_P == "225" && Convert.ToInt32(objActionIteams.ClaimStageID) == 14 && Data_refer_to_insurer.Rows[0]["RTI_reasonID"].ToString() == "221")
                        {
                            dtresult1 = _objMadicalScrutinyVM.USP_ClaimAudit_Insert_Refer_to_Insurer(objActionIteams, out msg1);
                            if (dtresult1.Tables.Count != 0)
                            {
                                if (Convert.ToInt16(JObject["RequestTypeID"]) == 1 || Convert.ToInt16(JObject["RequestTypeID"]) == 2 || Convert.ToInt16(JObject["RequestTypeID"]) == 3 && VVflag == 0 && dtresult1.Tables[1].Rows.Count > 0)
                                    msg1 = msg1 + " ; " + Save_ePreauthDetails(23, Convert.ToInt64(JObject["ClaimID"]), Convert.ToInt32(JObject["Slno"]));
                                if (dtresult1.Tables[0].Rows.Count > 0 && dtresult1.Tables[1].Rows.Count > 0 && dtresult1.Tables[2].Rows.Count > 0)
                                {
                                    _objCommon.CommunicationInsert_Common(ref dtresult1, Convert.ToInt64(JObject["ClaimID"]), Convert.ToInt32(JObject["Slno"]), Convert.ToInt64(Data_refer_to_insurer.Rows[0]["mainMemberID"].ToString()),
                                            Convert.ToInt64(Data_refer_to_insurer.Rows[0]["policyID"].ToString()), Convert.ToInt64(Data_refer_to_insurer.Rows[0]["providerID"].ToString()), Convert.ToInt32(Data_refer_to_insurer.Rows[0]["BrokerID"].ToString() == "" ? "0" : Data_refer_to_insurer.Rows[0]["BrokerID"].ToString()), Convert.ToInt64(Data_refer_to_insurer.Rows[0]["CorporateID"].ToString()),
                                            Convert.ToInt64(Data_refer_to_insurer.Rows[0]["PayerID"].ToString()), IssueId, 23, "MedicalScrutinyController", Convert.ToInt32(Session[SessionValue.UserRegionID]), 0, Convert.ToInt32(Data_refer_to_insurer.Rows[0]["AgentID"].ToString() == "" ? "0" : Data_refer_to_insurer.Rows[0]["AgentID"].ToString()));
                                    if (dtresult1.Tables[3].Rows.Count > 0)
                                    {
                                        DataSet dsResultforapproval = _objMadicalScrutinyVM.Get_Approvalletterdata(Convert.ToInt64(dtresult1.Tables[3].Rows[0]["ClaimID"]), Convert.ToInt16(dtresult1.Tables[3].Rows[0]["Slno"]), 1, 3, Convert.ToInt16(dtresult1.Tables[3].Rows[0]["RequestTypeID"]));
                                        if (dsResultforapproval.Tables.Count != 0)
                                        {
                                            if (dsResultforapproval.Tables[0].Rows.Count > 0 && dsResultforapproval.Tables[1].Rows.Count > 0)
                                            {
                                                _objCommon.CommunicationInsert_Common(ref dsResultforapproval, Convert.ToInt64(dtresult1.Tables[3].Rows[0]["ClaimID"]), Convert.ToInt16(dtresult1.Tables[3].Rows[0]["Slno"]), Convert.ToInt64(dtresult1.Tables[3].Rows[0]["MainMemberPolicyID"]),
                                                 Convert.ToInt64(dtresult1.Tables[3].Rows[0]["PolicyID"]), Convert.ToInt64(dtresult1.Tables[3].Rows[0]["ProviderID"]), Convert.ToInt32(dtresult1.Tables[3].Rows[0]["BrokerID"].ToString() == "" ? "0" : dtresult1.Tables[3].Rows[0]["BrokerID"]), Convert.ToInt64(dtresult1.Tables[3].Rows[0]["CorporateID"].ToString() == "" ? "0" : dtresult1.Tables[3].Rows[0]["CorporateID"]),
                                                 Convert.ToInt64(dtresult1.Tables[3].Rows[0]["PayerID"]), Convert.ToInt32(dtresult1.Tables[3].Rows[0]["InsuranceCompanyID"]), 23, "MedicalScrutinyController", Convert.ToInt32(Session[SessionValue.UserRegionID]), 0, Convert.ToInt32(dtresult1.Tables[3].Rows[0]["AgentID"].ToString() == "" ? "0" : dtresult1.Tables[3].Rows[0]["AgentID"]));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    //Claim Lock Release Code By Srinu B
                    new DefaultCacheProvider().Invalidate(Convert.ToString(JObject["ClaimID"]));


                    return Newtonsoft.Json.JsonConvert.SerializeObject(msg);
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "Adjudication_Actions_Insert", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        public string insertClaimRejectedreason(long claimID, int Slno, int stageID)
        {
            DataTable dt = _objMadicalScrutinyVM.Getcommuicationbasic_details(claimID, Slno);
            if (dt.Rows.Count > 0)
            {
                DataSet dtresult1 = _objMadicalScrutinyVM.Get_ClaimCommunicationForRejection(claimID, Convert.ToInt16(dt.Rows[0]["Slno"].ToString()), stageID, Convert.ToInt32(dt.Rows[0]["ClaimTypeID"].ToString()), Convert.ToInt32(dt.Rows[0]["RequesttypeID"].ToString()), Convert.ToInt32(dt.Rows[0]["PolicyTypeID"].ToString()), Convert.ToInt32(dt.Rows[0]["CorporateID"].ToString()));

                if (dtresult1.Tables.Count != 0)
                {
                    if (dtresult1.Tables[0].Rows.Count > 0 && dtresult1.Tables[1].Rows.Count > 0 && dtresult1.Tables[2].Rows.Count > 0)
                    {
                        _objCommon.CommunicationInsert_Common(ref dtresult1, claimID, Slno, Convert.ToInt64(dt.Rows[0]["mainMemberID"].ToString()),
                                Convert.ToInt64(dt.Rows[0]["policyID"].ToString()), Convert.ToInt64(dt.Rows[0]["providerID"].ToString()), Convert.ToInt32(dt.Rows[0]["BrokerID"].ToString() == "" ? "0" : dt.Rows[0]["BrokerID"].ToString()), Convert.ToInt64(dt.Rows[0]["CorporateID"].ToString()),
                                Convert.ToInt64(dt.Rows[0]["PayerID"].ToString()), Convert.ToInt32(dt.Rows[0]["IssueID"].ToString()), stageID, "MedicalScrutinyController", Convert.ToInt32(Session[SessionValue.UserRegionID]), 0, Convert.ToInt32(dt.Rows[0]["AgentID"].ToString() == "" ? "0" : dt.Rows[0]["AgentID"].ToString()), 1);

                    }
                }
            }
            return "";
        }

        public void update_rejction_letter(string ClaimID, string Slno, string ClaimRejections)
        {
            string msg = "";
            DataTable dtClaimRejections = GetClaimRejectionTableStructure(ClaimRejections);
            _objMadicalScrutinyVM.ClaimRejection_Validate(Convert.ToInt64(ClaimID), Convert.ToInt32(Slno), dtClaimRejections, 0, Convert.ToDecimal(0), Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]), Convert.ToInt32(Session[Resources.SessionValue.RegionID]), Convert.ToInt32(225), out msg);
            if (msg.Contains("Saved Successfully"))
                insertClaimRejectedreason(Convert.ToInt64(ClaimID), Convert.ToInt16(Slno), 23);
        }

        public void update_crc_letter(string ClaimID, string Slno, string ClaimRejections)
        {
            string msg = "";
            DataTable dtClaimRejections = GetClaimRejectionTableStructure(ClaimRejections);
            _objMadicalScrutinyVM.ClaimRejection_Validate(Convert.ToInt64(ClaimID), Convert.ToInt32(Slno), dtClaimRejections, 0, Convert.ToDecimal(0), Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]), Convert.ToInt32(Session[Resources.SessionValue.RegionID]), Convert.ToInt32(225), out msg);
            if (msg.Contains("Saved Successfully"))
                insertClaimRejectedreason(Convert.ToInt64(ClaimID), Convert.ToInt16(Slno), 17);
        }

        //Abhishek 15 Feb 2023 calling the reliance push api asynchornously
        //private async Task<string> PushDataToReliance(long ClaimID, int SlNo, string Remarks, int RequestTypeID, int IssueId)
        //{
        //    return await Task.Run(() =>
        //    {
        //        return _objMadicalScrutinyVM.PushWebServiceBasedOnPre_Auth_No(ClaimID, SlNo, Remarks, RequestTypeID, IssueId);
        //    });
        //}
        public string ClaimAudit_Insert(string ClaimDetails, bool isApprove, string PolicyType, string MainMemberPolicyID, string PolicyID, string ProviderID,
            string BrokerID, string PayerID, string CorporateID, string InsuranceCompanyID, decimal excesssuminsured, decimal SanctionedAmount, string QMSID, string QMSAdminID, bool? skipAudit, int? createnewtop = 0, string Isrefertocrm = "0")
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    int ClaimsStageID = Convert.ToInt32(Resources.StageIDs.Audit);//24

                    long ClmID = 0;
                    byte serialno;
                    string msg = string.Empty;
                    string vmessage;
                    decimal NetPayableAmt = SanctionedAmount;

                    string QMS = string.Empty;
                    QMS = QMSID;
                    string QMSadmin = string.Empty;
                    QMSadmin = QMSAdminID;
                    Qmsv2CMController qms = new Qmsv2CMController();

                    //  DataTable lst1 = (DataTable)JsonConvert.DeserializeObject(ClaimDetails, (typeof(DataTable)));
                    Newtonsoft.Json.Linq.JObject JObject = Newtonsoft.Json.Linq.JObject.Parse(ClaimDetails);
                    ClaimActionItems objActionIteams = new ClaimActionItems();
                    objActionIteams.ClaimID = Convert.ToInt64(JObject["ClaimID"]);
                    objActionIteams.CorpID = Convert.ToInt32(CorporateID);
                    objActionIteams.Slno = Convert.ToInt16(JObject["Slno"]);
                    objActionIteams.ClaimTypeID = Convert.ToInt16(JObject["ClaimTypeID"]);
                    objActionIteams.RequestTypeID = Convert.ToInt16(JObject["RequestTypeID"]);
                    objActionIteams.ServiceTypeID = Convert.ToInt16(JObject["ServiceTypeID"]);
                    objActionIteams.ServiceSubTypeID = Convert.ToInt16(JObject["ServiceSubTypeID"]);
                    objActionIteams.ClaimStageID = Convert.ToInt32(JObject["ClaimStageID"]);
                    objActionIteams.RoleID = Convert.ToInt32(JObject["RoleID"]);
                    objActionIteams.RegionID = Convert.ToInt32(Session[Resources.SessionValue.RegionID]);
                    objActionIteams.ClaimedAmount = Convert.ToDecimal(JObject["ClaimedAmount"]);
                    objActionIteams.ReasonIDs_P = Convert.ToString(JObject["ReasonIDs_P"]);
                    objActionIteams.Remarks = Convert.ToString(JObject["Remarks"]);
                    objActionIteams.ClosedBy = Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]);
                    string PayeeType = Convert.ToString(JObject["PayeeType"]);
                    int IssueId = Convert.ToInt16(JObject["issueID"]);
                    if (IssueId <= 0)
                        IssueId = _objMadicalScrutinyVM.GetIssueID(objActionIteams.ClaimID);
                    if (NetPayableAmt <= 0)
                        NetPayableAmt = _objMadicalScrutinyVM.GetSanctionedAmount(objActionIteams.ClaimID, objActionIteams.Slno);
                    //_objMadicalScrutinyVM.ClaimAudit_Insert(objActionIteams, isApprove, out msg);
                    DataSet dsResult = null;
                    string ApiResponse = string.Empty;

                    if (IssueId != 9)
                    {
                        //SP3V-3049,3050 Move the stage to Refer to insuer on click of Audit approval Abhishek w.e.f. 14 Sep 23
                        string issueIds = Convert.ToString(ConfigurationManager.AppSettings["OnAuditReferToInsurer"]);
                        int[] lstIssueId = Array.ConvertAll(issueIds.Split(','), int.Parse);
                        //bool Actionremarks = false;
                        //if (IssueId == 10)
                        //    Actionremarks = _objMadicalScrutinyVM.getClaimActionRemarks(Convert.ToInt64(JObject["ClaimID"]), Convert.ToInt16(JObject["Slno"]));

                        //if (IssueId == 10 && isApprove && (Convert.ToInt16(JObject["RequestTypeID"]) == 1 || Convert.ToInt16(JObject["RequestTypeID"]) == 2 || Convert.ToInt16(JObject["RequestTypeID"]) == 3) && Actionremarks)//GoDigit,ITIC=>Iffco Tokio
                        //{

                        //    dsResult = _objMadicalScrutinyVM.ClaimAudit_Validate(objActionIteams, isApprove, out msg, Convert.ToInt16(PolicyType), PayeeType, Convert.ToString(JObject["NomineePayeeName"]));
                        //    if (string.IsNullOrEmpty(msg)) //Claim is valid, now send the claim to Refer to Insuer and push the data
                        //        ReferToInsurer(out msg, objActionIteams, out dsResult);
                        //}
                        bool isRestrictedITCIPolicyNo = GetIsRestrictedPolicyNo(PolicyID, IssueId);
                        DataTable is_refer_to_insurer = _objMadicalScrutinyVM.Check_Insurer_Refer_to_insurer(Convert.ToInt64(objActionIteams.ClaimID), Convert.ToInt32(objActionIteams.Slno), 1);
                        if ((IssueId == 31 || (!isRestrictedITCIPolicyNo && IssueId == 10 && objActionIteams.RequestTypeID != 1 && objActionIteams.RequestTypeID != 2 && objActionIteams.RequestTypeID != 3) || (is_refer_to_insurer.Rows[0]["Isrefertoinsurer"].ToString() == "True")) && isApprove)//GoDigit
                        {
                            objActionIteams.ReasonIDs_P = "220";
                            dsResult = _objMadicalScrutinyVM.ClaimAudit_Validate(objActionIteams, isApprove, out msg, Convert.ToInt16(PolicyType), PayeeType, Convert.ToString(JObject["NomineePayeeName"]));
                            if (string.IsNullOrEmpty(msg)) //Claim is valid, now send the claim to Refer to Insuer and push the data
                                ReferToInsurer(out msg, objActionIteams, out dsResult);

                            // pushing to itgi
                            if (IssueId == 10)
                            {
                                msg = ITGI_RefertoInsurerAtForAuditStage(Convert.ToInt64(objActionIteams.ClaimID), Convert.ToInt32(objActionIteams.Slno));
                            }
                            msg = msg + "  " + "Claim moved to refer to insurer stage.";
                        }
                        else if (Isrefertocrm == "1" && isApprove)
                        {
                            dsResult = _objMadicalScrutinyVM.ClaimAudit_Validate(objActionIteams, isApprove, out msg, Convert.ToInt16(PolicyType), PayeeType, Convert.ToString(JObject["NomineePayeeName"]), Convert.ToString(skipAudit), Isrefertocrm);
                        }
                        else
                            dsResult = _objMadicalScrutinyVM.ClaimAudit_Insert(objActionIteams, isApprove, out msg, Convert.ToInt16(PolicyType), PayeeType, Convert.ToString(JObject["NomineePayeeName"]), skipAudit, createnewtop);


                        qms.UpdateClaimStatus("UPDATESTATUS", "", "", "", "", QMS, "5", Session["UserRegionID"].ToString());
                        //if ((Convert.ToInt32(CorporateID) == 23508 || Convert.ToInt32(CorporateID) == 23509 || Convert.ToInt32(CorporateID) == 23510) && dsResult.Tables.Count != 0 && excesssuminsured > 0 && (Convert.ToInt16(JObject["RequestTypeID"]) == 1 || Convert.ToInt16(JObject["RequestTypeID"]) == 2 || Convert.ToInt16(JObject["RequestTypeID"]) == 3))
                        //{
                        //    _objMadicalScrutinyVM.Createtopclaim(Convert.ToInt64(JObject["ClaimID"]), Convert.ToInt16(JObject["Slno"]), Convert.ToInt16(JObject["ClaimTypeID"]), Convert.ToInt16(JObject["RequestTypeID"]), Convert.ToInt16(JObject["ServiceTypeID"]),
                        //        Convert.ToInt16(JObject["ServiceSubTypeID"]), Convert.ToInt32(JObject["RoleID"]), Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]), Convert.ToInt32(Session[Resources.SessionValue.RegionID]), Convert.ToInt64(PolicyID), out vmessage, out ClmID, out serialno);
                        //    msg = msg + vmessage;
                        //}
                        if (dsResult.Tables.Count != 0)
                        {
                            if (Convert.ToInt16(JObject["RequestTypeID"]) == 1 || Convert.ToInt16(JObject["RequestTypeID"]) == 2 || Convert.ToInt16(JObject["RequestTypeID"]) == 3 && VVflag == 0 && dsResult.Tables[1].Rows.Count > 0)
                                msg = msg + " ; " + Save_ePreauthDetails(24, Convert.ToInt64(JObject["ClaimID"]), Convert.ToInt32(JObject["Slno"]));
                            if (dsResult.Tables[0].Rows.Count > 0 && dsResult.Tables[1].Rows.Count > 0)//&& dsResult.Tables[2].Rows.Count > 0)
                            {
                                ////CommunicatingQuerypending(ref dsResult, Convert.ToInt64(ClaimID), Convert.ToByte(SlNo), Convert.ToInt64(MainMemberPolicyID), Convert.ToInt64(PolicyID), Convert.ToInt64(ProviderID), Convert.ToInt32(BrokerID), Convert.ToInt64(CorporateID), Convert.ToInt64(PayerID), Convert.ToInt32(InsuranceCompanyID));
                                if (ClmID == 0)
                                    _objCommon.CommunicationInsert_Common(ref dsResult, Convert.ToInt64(JObject["ClaimID"]), Convert.ToInt32(JObject["Slno"]), Convert.ToInt64(MainMemberPolicyID),
                                        Convert.ToInt64(PolicyID), Convert.ToInt64(ProviderID), Convert.ToInt32(BrokerID == "" ? "0" : BrokerID), Convert.ToInt64(CorporateID),
                                        Convert.ToInt64(PayerID), Convert.ToInt32(InsuranceCompanyID), ClaimsStageID, "MedicalScrutinyController", Convert.ToInt32(Session[SessionValue.UserRegionID]), 0, Convert.ToInt32(JObject["AgentID"].ToString() == "" ? "0" : JObject["AgentID"].ToString()));

                            }
                        }

                    }
                    if (IssueId == 9)
                    {
                        if (!isApprove)  //If the claim is not approved and again sent back to adjudication level
                        {
                            dsResult = _objMadicalScrutinyVM.ClaimAudit_Insert(objActionIteams, isApprove, out msg, Convert.ToInt16(PolicyType), PayeeType, Convert.ToString(JObject["NomineePayeeName"]), skipAudit);
                            qms.UpdateClaimStatus("UPDATESTATUS", "", "", "", "", QMS, "5", Session["UserRegionID"].ToString());
                            if (dsResult.Tables.Count != 0)
                            {
                                if (Convert.ToInt16(JObject["RequestTypeID"]) == 1 || Convert.ToInt16(JObject["RequestTypeID"]) == 2 || Convert.ToInt16(JObject["RequestTypeID"]) == 3 && VVflag == 0 && dsResult.Tables[1].Rows.Count > 0)
                                    msg = msg + " ; " + Save_ePreauthDetails(24, Convert.ToInt64(JObject["ClaimID"]), Convert.ToInt32(JObject["Slno"]));
                                if (dsResult.Tables[0].Rows.Count > 0 && dsResult.Tables[1].Rows.Count > 0)//&& dsResult.Tables[2].Rows.Count > 0)
                                {
                                    if (ClmID == 0)
                                        _objCommon.CommunicationInsert_Common(ref dsResult, Convert.ToInt64(JObject["ClaimID"]), Convert.ToInt32(JObject["Slno"]), Convert.ToInt64(MainMemberPolicyID),
                                            Convert.ToInt64(PolicyID), Convert.ToInt64(ProviderID), Convert.ToInt32(BrokerID == "" ? "0" : BrokerID), Convert.ToInt64(CorporateID),
                                            Convert.ToInt64(PayerID), Convert.ToInt32(InsuranceCompanyID), ClaimsStageID, "MedicalScrutinyController", Convert.ToInt32(Session[SessionValue.UserRegionID]), 0, Convert.ToInt32(JObject["AgentID"].ToString() == "" ? "0" : JObject["AgentID"].ToString()));

                                }
                            }
                        }
                        if (isApprove) //If Claims is approved and send for Audit
                        {
                            #region reliance duplicate code
                            //If the insurer is Reliance, need to send for Refer to insurer
                            //First validate the Audit details once valid, send the claim to Refer to Insuer and push the data

                            //If the Claim amt is <=25000 send the data to insuere but move the claim for payment and update the remark as Claim Data sent to insurer w.e.f. 11 Apr 23 (as per instruction from Srini)
                            //if (NetPayableAmt <= 25000)
                            //{
                            //    dsResult = _objMadicalScrutinyVM.ClaimAudit_Insert(objActionIteams, isApprove, out msg, Convert.ToInt16(PolicyType), PayeeType, Convert.ToString(JObject["NomineePayeeName"]), skipAudit);
                            //    qms.UpdateClaimStatus("UPDATESTATUS", "", "", "", "", QMS, "5", Session["UserRegionID"].ToString());
                            //    if (dsResult.Tables.Count != 0)
                            //    {
                            //        if (Convert.ToInt16(JObject["RequestTypeID"]) == 1 || Convert.ToInt16(JObject["RequestTypeID"]) == 2 || Convert.ToInt16(JObject["RequestTypeID"]) == 3 && VVflag == 0 && dsResult.Tables[1].Rows.Count > 0)
                            //            msg = msg + " ; " + Save_ePreauthDetails(24, Convert.ToInt64(JObject["ClaimID"]), Convert.ToInt32(JObject["Slno"]));
                            //        if (dsResult.Tables[0].Rows.Count > 0 && dsResult.Tables[1].Rows.Count > 0)//&& dsResult.Tables[2].Rows.Count > 0)
                            //        {
                            //            ////CommunicatingQuerypending(ref dsResult, Convert.ToInt64(ClaimID), Convert.ToByte(SlNo), Convert.ToInt64(MainMemberPolicyID), Convert.ToInt64(PolicyID), Convert.ToInt64(ProviderID), Convert.ToInt32(BrokerID), Convert.ToInt64(CorporateID), Convert.ToInt64(PayerID), Convert.ToInt32(InsuranceCompanyID));
                            //            if (ClmID == 0)
                            //                _objCommon.CommunicationInsert_Common(ref dsResult, Convert.ToInt64(JObject["ClaimID"]), Convert.ToInt32(JObject["Slno"]), Convert.ToInt64(MainMemberPolicyID),
                            //                    Convert.ToInt64(PolicyID), Convert.ToInt64(ProviderID), Convert.ToInt32(BrokerID == "" ? "0" : BrokerID), Convert.ToInt64(CorporateID),
                            //                    Convert.ToInt64(PayerID), Convert.ToInt32(InsuranceCompanyID), ClaimsStageID, "MedicalScrutinyController", Convert.ToInt32(Session[SessionValue.UserRegionID]), 0, Convert.ToInt32(JObject["AgentID"].ToString() == "" ? "0" : JObject["AgentID"].ToString()));

                            //        }
                            //    }
                            //    ApiResponse = _objMadicalScrutinyVM.PushWebServiceBasedOnPre_Auth_No(objActionIteams.ClaimID, objActionIteams.Slno, objActionIteams.Remarks, objActionIteams.RequestTypeID, IssueId, objActionIteams.ClaimedAmount);
                            //    _objMadicalScrutinyVM.UpdateRelianceFailureRemark(objActionIteams.ClaimID, objActionIteams.Slno, "Claim Data sent to insurer");
                            //}

                            /* 
                             * Changed by Abhishek w.e.f. 25 May 23, Ref Jira# sp3v_2384 
                             * if the claimType is cashless and amt >25K & Request Type is preauth type, then no need to call pushAPI and change status to either cashless approved Stage or ForPayment stage
                             */

                            //if (NetPayableAmt > 25000)
                            //{
                            //    int RequestType = Convert.ToInt16(JObject["RequestTypeID"]);
                            //    if (objActionIteams.ClaimTypeID == 1 && (RequestType == 1 || RequestType == 2 || RequestType == 3)) //For cashless with amt>25K, no need to call pushAPI
                            //    {
                            //        dsResult = _objMadicalScrutinyVM.ClaimAudit_Insert(objActionIteams, isApprove, out msg, Convert.ToInt16(PolicyType), PayeeType, Convert.ToString(JObject["NomineePayeeName"]), skipAudit);
                            //        qms.UpdateClaimStatus("UPDATESTATUS", "", "", "", "", QMS, "5", Session["UserRegionID"].ToString());
                            //        if (dsResult.Tables.Count != 0)
                            //        {
                            //            if (Convert.ToInt16(JObject["RequestTypeID"]) == 1 || Convert.ToInt16(JObject["RequestTypeID"]) == 2 || Convert.ToInt16(JObject["RequestTypeID"]) == 3 && VVflag == 0 && dsResult.Tables[1].Rows.Count > 0)
                            //                msg = msg + " ; " + Save_ePreauthDetails(24, Convert.ToInt64(JObject["ClaimID"]), Convert.ToInt32(JObject["Slno"]));
                            //            if (dsResult.Tables[0].Rows.Count > 0 && dsResult.Tables[1].Rows.Count > 0)
                            //            {
                            //                if (ClmID == 0)
                            //                    _objCommon.CommunicationInsert_Common(ref dsResult, Convert.ToInt64(JObject["ClaimID"]), Convert.ToInt32(JObject["Slno"]), Convert.ToInt64(MainMemberPolicyID),
                            //                        Convert.ToInt64(PolicyID), Convert.ToInt64(ProviderID), Convert.ToInt32(BrokerID == "" ? "0" : BrokerID), Convert.ToInt64(CorporateID),
                            //                        Convert.ToInt64(PayerID), Convert.ToInt32(InsuranceCompanyID), ClaimsStageID, "MedicalScrutinyController", Convert.ToInt32(Session[SessionValue.UserRegionID]), 0, Convert.ToInt32(JObject["AgentID"].ToString() == "" ? "0" : JObject["AgentID"].ToString()));

                            //            }
                            //        }
                            //        //Call the push API and irrespective of response status send the claim for "Cashless Approved" or "For Payment" stage.
                            //        ApiResponse = _objMadicalScrutinyVM.PushWebServiceBasedOnPre_Auth_No(objActionIteams.ClaimID, objActionIteams.Slno, objActionIteams.Remarks, objActionIteams.RequestTypeID, IssueId, objActionIteams.ClaimedAmount);
                            //        _objMadicalScrutinyVM.UpdateRelianceFailureRemark(objActionIteams.ClaimID, objActionIteams.Slno, ApiResponse);
                            //    }
                            //    else
                            //    {
                            //        dsResult = _objMadicalScrutinyVM.ClaimAudit_Validate(objActionIteams, isApprove, out msg, Convert.ToInt16(PolicyType), PayeeType, Convert.ToString(JObject["NomineePayeeName"]));
                            //        qms.UpdateClaimStatus("UPDATESTATUS", "", "", "", "", QMS, "5", Session["UserRegionID"].ToString());
                            //        if (string.IsNullOrEmpty(msg)) //Claim is valid, now send the claim to Refer to Insuer and push the data
                            //        {
                            //            ApiResponse = _objMadicalScrutinyVM.PushWebServiceBasedOnPre_Auth_No(objActionIteams.ClaimID, objActionIteams.Slno, objActionIteams.Remarks, objActionIteams.RequestTypeID, IssueId, objActionIteams.ClaimedAmount);
                            //            ReferToInsurer(out msg, objActionIteams, out dsResult);
                            //            _objMadicalScrutinyVM.UpdateRelianceFailureRemark(objActionIteams.ClaimID, objActionIteams.Slno, ApiResponse);
                            //            return "API request failed. Claim remains in Refer to Insurer stage:";
                            //        }
                            //    }
                            //}
                            #endregion
                            int RequestType = Convert.ToInt16(JObject["RequestTypeID"]);
                            string reliance_nonAPI_policies = ConfigurationManager.AppSettings["reliance_nonAPI_policies"].ToString().Trim().TrimStart(',');
                            bool relianceNIDB_Flag = false;
                            if ((RequestType == 1 || RequestType == 2 || RequestType == 3) && (Convert.ToBoolean(JObject["IsPolicyNIDB"]) == true || Convert.ToBoolean(JObject["IsNIDB"]) == true))
                                relianceNIDB_Flag = true;
                            if (NetPayableAmt > 0)
                            {
                                if ((objActionIteams.ClaimTypeID == 1 && (RequestType == 4)) || (reliance_nonAPI_policies.Split(',').Contains(PolicyID)) || relianceNIDB_Flag == true)
                                {
                                    dsResult = _objMadicalScrutinyVM.ClaimAudit_Insert(objActionIteams, isApprove, out msg, Convert.ToInt16(PolicyType), PayeeType, Convert.ToString(JObject["NomineePayeeName"]), skipAudit);
                                    qms.UpdateClaimStatus("UPDATESTATUS", "", "", "", "", QMS, "5", Session["UserRegionID"].ToString());
                                    if (dsResult.Tables.Count != 0)
                                    {
                                        if (Convert.ToInt16(JObject["RequestTypeID"]) == 1 || Convert.ToInt16(JObject["RequestTypeID"]) == 2 || Convert.ToInt16(JObject["RequestTypeID"]) == 3 && VVflag == 0 && dsResult.Tables[1].Rows.Count > 0)
                                            msg = msg + " ; " + Save_ePreauthDetails(24, Convert.ToInt64(JObject["ClaimID"]), Convert.ToInt32(JObject["Slno"]));
                                        if (dsResult.Tables[0].Rows.Count > 0 && dsResult.Tables[1].Rows.Count > 0)
                                        {
                                            if (ClmID == 0)
                                                _objCommon.CommunicationInsert_Common(ref dsResult, Convert.ToInt64(JObject["ClaimID"]), Convert.ToInt32(JObject["Slno"]), Convert.ToInt64(MainMemberPolicyID),
                                                    Convert.ToInt64(PolicyID), Convert.ToInt64(ProviderID), Convert.ToInt32(BrokerID == "" ? "0" : BrokerID), Convert.ToInt64(CorporateID),
                                                    Convert.ToInt64(PayerID), Convert.ToInt32(InsuranceCompanyID), ClaimsStageID, "MedicalScrutinyController", Convert.ToInt32(Session[SessionValue.UserRegionID]), 0, Convert.ToInt32(JObject["AgentID"].ToString() == "" ? "0" : JObject["AgentID"].ToString()));

                                        }
                                    }
                                }
                                else
                                {
                                    dsResult = _objMadicalScrutinyVM.ClaimAudit_Validate(objActionIteams, isApprove, out msg, Convert.ToInt16(PolicyType), PayeeType, Convert.ToString(JObject["NomineePayeeName"]));
                                    qms.UpdateClaimStatus("UPDATESTATUS", "", "", "", "", QMS, "5", Session["UserRegionID"].ToString());
                                    if (string.IsNullOrEmpty(msg)) //Claim is valid, now send the claim to Refer to Insuer and push the data
                                    {
                                        string enviroment = System.Web.Configuration.WebConfigurationManager.AppSettings["Enviroment"].ToString().ToLower();
                                        if (enviroment == "production")
                                            ApiResponse = _objMadicalScrutinyVM.PushWebServiceBasedOnPre_Auth_No(objActionIteams.ClaimID, objActionIteams.Slno, objActionIteams.Remarks, objActionIteams.RequestTypeID, IssueId, objActionIteams.ClaimedAmount);
                                        ReferToInsurer(out msg, objActionIteams, out dsResult);
                                        _objMadicalScrutinyVM.UpdateRelianceFailureRemark(objActionIteams.ClaimID, objActionIteams.Slno, ApiResponse);
                                        return "API request failed. Claim remains in Refer to Insurer stage:";
                                    }
                                }
                            }
                        }

                    }
                    Task<string> task;
                    if (objActionIteams.RequestTypeID == 1 || objActionIteams.RequestTypeID == 2 || objActionIteams.RequestTypeID == 3)
                    {
                        DataTable dt = _objMadicalScrutinyVM.getcorlidfromdb(objActionIteams.ClaimID, objActionIteams.Slno, 1);
                        if (dt.Rows.Count > 0)
                        {
                            if (dt.Rows[0]["RequestType"].ToString() == "6")
                                task = _objMadicalScrutinyVM.PreauthOnSubmitBundle(dt.Rows[0]["CorelationId"].ToString(), 3);
                            else
                            {
                                task = _objMadicalScrutinyVM.PreauthOnSubmitBundle(dt.Rows[0]["CorelationId"].ToString(), 1);
                            }
                        }
                    }
                    else if (objActionIteams.RequestTypeID == 4)
                    {
                        DataTable dt = _objMadicalScrutinyVM.getcorlidfromdb(objActionIteams.ClaimID, objActionIteams.Slno, 4);
                        if (dt.Rows.Count > 0)
                        {
                            if (dt.Rows[0]["RequestType"].ToString() == "6")
                                task = _objMadicalScrutinyVM.PreauthOnSubmitBundle(dt.Rows[0]["CorelationId"].ToString(), 3);
                            else
                                task = _objMadicalScrutinyVM.PreauthOnSubmitBundle(dt.Rows[0]["CorelationId"].ToString(), 2);
                        }
                    }
                    //Claim Lock Release Code By Srinu B
                    new DefaultCacheProvider().Invalidate(Convert.ToString(JObject["ClaimID"]));
                    //SP3V-2447 Leena------------------



                    return Newtonsoft.Json.JsonConvert.SerializeObject(msg);
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "ClaimAudit_Insert", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }
        private void ReferToInsurer(out string msg, ClaimActionItems objActionIteams, out DataSet dsResult)
        {
            if (string.IsNullOrEmpty(objActionIteams.ReasonIDs_P))
                objActionIteams.ReasonIDs_P = "220";
            objActionIteams.ReasonIDs_P = objActionIteams.ReasonIDs_P;// "220";//Request for Approval
            objActionIteams.ClaimStageID = 17;// Refer to Insurer
            DataTable dtClaimRejections = GetClaimRejectionTableStructure("");
            dsResult = _objMadicalScrutinyVM.Adjudication_Actions_Insert(objActionIteams, dtClaimRejections, out msg);
        }

        /* End Srividya Code*/

        /* Start Nagaraju Code*/
        /* Query Documents */
        public string Get_QueryDetails(string ClaimID, string SlNo)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    var data = Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.Get_QueryDetailsVM(Convert.ToInt64(ClaimID), Convert.ToInt16(SlNo)));
                    return data;
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "Get_QueryDetails", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        public string Save_QueryPendingDetails(string ClaimID, string SlNo, string ClaimsIRReasons, string ClaimTypeID, string RequestTypeID,
            string ServiceTypeID, string ServiceSubTypeID, string ClaimedAmount, string SITypeID, string MainMemberPolicyID, string PolicyID,
            string ProviderID, string BrokerID, string PayerID, string CorporateID, string InsuranceCompanyID, string PolicyType, string ClaimCurrentStageID, int AgentID,
            string QMSID, string QMSAdminID)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    int ClaimsStageID = 0;
                    if (Convert.ToInt32(ClaimTypeID) == 1)
                    {
                        StageId = Convert.ToByte(Resources.StageIDs.QueryToHospital);//7
                        ClaimsStageID = Convert.ToInt32(Resources.StageIDs.QueryToHospital);//7
                    }

                    else if (Convert.ToInt32(ClaimTypeID) == 2)
                    {
                        StageId = Convert.ToByte(Resources.StageIDs.QueryToMember);//8
                        ClaimsStageID = Convert.ToInt32(Resources.StageIDs.QueryToMember);//8
                    }


                    DataTable dtClaimsIRReasons = (DataTable)JsonConvert.DeserializeObject(ClaimsIRReasons, (typeof(DataTable)));

                    string vMessage = string.Empty;


                    //Code Changed By Srinu B
                    //int id = _objMadicalScrutinyVM.Save_QueryPendingDetailsVM(Convert.ToInt64(ClaimID), Convert.ToInt16(SlNo), dtClaimsIRReasons, Convert.ToInt32(ClaimTypeID),
                    //   Convert.ToInt32(RequestTypeID), Convert.ToInt32(ServiceTypeID), Convert.ToInt32(ServiceSubTypeID), ClaimsStageID, ClaimIRRoleID,
                    //   Convert.ToInt32(Session[Resources.SessionValue.RegionID]), Convert.ToDecimal(ClaimedAmount), Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]), out vMessage);
                    DataSet dsResult = null;
                    dsResult = _objMadicalScrutinyVM.Save_QueryPendingDetailsVM(Convert.ToInt64(ClaimID), Convert.ToInt16(SlNo), dtClaimsIRReasons, Convert.ToInt32(ClaimTypeID),
                       Convert.ToInt32(RequestTypeID), Convert.ToInt32(ServiceTypeID), Convert.ToInt32(ServiceSubTypeID), ClaimsStageID, ClaimIRRoleID,
                       Convert.ToInt32(Session[Resources.SessionValue.RegionID]), Convert.ToDecimal(ClaimedAmount), Convert.ToInt16(PolicyType), Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]), Convert.ToInt16(ClaimCurrentStageID), Convert.ToInt32(CorporateID), out vMessage);

                    string QMS = string.Empty;
                    QMS = QMSID;
                    string QMSadmin = string.Empty;
                    QMSadmin = QMSAdminID;

                    Qmsv2CMController qms = new Qmsv2CMController();
                    qms.UpdateClaimStatus("UPDATESTATUS", "", "", "", "", QMS, "5", Session["UserRegionID"].ToString());

                    //Communication
                    if (dsResult.Tables.Count != 0)
                    {
                        if ((Convert.ToInt32(RequestTypeID) == 1 || Convert.ToInt32(RequestTypeID) == 2 || Convert.ToInt32(RequestTypeID) == 3) && dsResult.Tables[1].Rows.Count > 0)
                            vMessage = vMessage + " ; " + Save_ePreauthDetails(7, Convert.ToInt64(ClaimID), Convert.ToInt32(SlNo));
                        if (dsResult.Tables[0].Rows.Count > 0 && dsResult.Tables[1].Rows.Count > 0 && dsResult.Tables[2].Rows.Count > 0)
                        {
                            ////CommunicatingQuerypending(ref dsResult, Convert.ToInt64(ClaimID), Convert.ToByte(SlNo), Convert.ToInt64(MainMemberPolicyID), Convert.ToInt64(PolicyID), Convert.ToInt64(ProviderID), Convert.ToInt32(BrokerID), Convert.ToInt64(CorporateID), Convert.ToInt64(PayerID), Convert.ToInt32(InsuranceCompanyID));
                            _objCommon.CommunicationInsert_Common(ref dsResult, Convert.ToInt64(ClaimID), Convert.ToInt32(SlNo), Convert.ToInt64(MainMemberPolicyID),
                                Convert.ToInt64(PolicyID), Convert.ToInt64(ProviderID), Convert.ToInt32(BrokerID), Convert.ToInt64(CorporateID),
                                Convert.ToInt64(PayerID), Convert.ToInt32(InsuranceCompanyID), ClaimsStageID, "MedicalScrutinyController", Convert.ToInt32(Session[SessionValue.UserRegionID]), 0, AgentID, 0, RequestTypeID);

                        }
                    }

                    int rows = dtClaimsIRReasons.AsEnumerable().Where(b => b.Field<Int64>("isReceived") == 1).Count();
                    if (rows == 0)
                    {
                        int calling_flag = 1;
                        if (Convert.ToInt32(RequestTypeID) == 4) calling_flag = 4;
                        else calling_flag = 1;

                        DataTable dt = _objMadicalScrutinyVM.getcorlidfromdb(Convert.ToInt64(ClaimID), Convert.ToInt16(SlNo), calling_flag);
                        if (dt.Rows.Count > 0)
                        {
                            string isSubmitted = _objMadicalScrutinyVM.nhcxSubmitBundle(Convert.ToInt64(ClaimID), Convert.ToInt16(SlNo), 2);

                            if (isSubmitted != "Request sent to NHCX Successfully")
                            {
                                _objMadicalScrutinyVM.claimqueryrollback(Convert.ToInt64(ClaimID), Convert.ToInt16(SlNo));
                            }
                            ;
                            vMessage = vMessage + isSubmitted;
                        }
                    }

                    //Code Changed By Srinu B End

                    //Claim Lock Release Code By Srinu B
                    new DefaultCacheProvider().Invalidate(ClaimID);
                    //cacheobj.Invalidate(ClaimID);

                    return vMessage;
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "ClaimsController", "Save_QueryPendingDetails", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }


        /* End Nagaraju Code*/

        //Claims Coding     --Allu Srinu         
        public string Save_CodingDetails(string ClaimsCoding, string ClaimId, string SlNo, string BillingType)
        //public string Save_CodingDetails(string ClaimsCoding, string ClaimId, string SlNo, string BillingType)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    DataTable vDataTable = (DataTable)JsonConvert.DeserializeObject(ClaimsCoding, (typeof(DataTable)));

                    string vMessage = string.Empty;
                    int id = _objMadicalScrutinyVM.Save_CodingDetails(Convert.ToInt64(ClaimId), Convert.ToInt16(SlNo), Convert.ToInt32(BillingType), vDataTable,
                        Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]), out vMessage);

                    return vMessage;
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutiny", "Save_CodingDetails", Session[Resources.SessionValue.LoginUserID].ToString());
                //return ex.Message;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        [HttpGet]
        [Authorize]
        public string ClaimCodingDetails_Retrieve(long ClaimID, int SlNo, int ClaimReqTypeID, bool IsFrmArchived = false)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.ClaimCodingDetails_Retrieve(ClaimID, SlNo, ClaimReqTypeID, IsFrmArchived));
                }
                else
                {
                    //_objCommon.ErrorLog_Insert("Session Expired", "ClaimsController", "InsertPreauthRequest-Claim DashBoard");
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutiny", "ClaimCoding_Retrieve", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        [HttpGet]
        [Authorize]
        public string GetPackageRate_ClaimsCoding(long ProviderID, int ProcedureID, int IssueID, long CorpID, long PayerID, long PolicyID, int BrokerID, long ClaimID,
            long MemberPolicyID, int SITypeID, int Level1, byte isGipsa, byte isCI, byte isPED, byte isDayCare, int Slno)
        {
            try
            {
                string vMessage = string.Empty;
                if (Session[SessionValue.UserRegionID] != null)
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.GetPackageRate_ClaimsCoding(ProviderID, ProcedureID, IssueID,
                            CorpID, PayerID, PolicyID, BrokerID, ClaimID, MemberPolicyID, SITypeID, Level1, isGipsa, isCI, isPED, isDayCare, Slno));
                else
                    return "ErrorCode#1";
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "ClaimsController", "GetPackageRate_ClaimsCoding", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                ReturnError rtnObj = new ReturnError();
                rtnObj.ID = 1;
                rtnObj.Message = ex.Message;
                return Newtonsoft.Json.JsonConvert.SerializeObject(rtnObj);
            }
        }

        public string IcdCode_validation(int IcdCode) // added by vsvskprasad 4261
        {
            string msg = "";
            try
            {
                DataSet ds = new DataSet();
                msg = new CommonBL().IcdCode_validation(IcdCode, out msg);
                return msg;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        [Authorize]
        public string GetPackageRate_PEDCIGIPSA(string ProcedureID, string IssueID)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.GetPackageRate_PEDCIGIPSAVM(Convert.ToInt32(ProcedureID), Convert.ToInt32(IssueID)));
                else
                    return "ErrorCode#1";
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "ClaimsController", "GetPackageRate_PEDCIGIPSA", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        [Authorize]
        public string Delete_CodingProcedure(string IDS)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    string vMessage = string.Empty;
                    int id = _objMadicalScrutinyVM.Delete_CodingProcedureVM(IDS);

                    return id.ToString();
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutiny", "Delete_CodingProcedure", Session[Resources.SessionValue.LoginUserID].ToString());
                //return "ErrorCode#1";
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }


        //Claims Coding     --Allu Srinu ---End 


        // Claim Rejected Reasons
        [Authorize]
        public string Get_ClaimRejectedReasons(string ClaimID, string SlNo)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    var data = Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.Get_ClaimRejectedReasonsVM(Convert.ToInt64(ClaimID), Convert.ToInt16(SlNo)));
                    return data;
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "Get_ClaimRejectedReasons", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        [Authorize]
        public JsonResult GetAdjudicatorLetterAccess(long ClaimID, short SlNo)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] == null)
                {
                    return Json(false, JsonRequestBehavior.AllowGet);
                }

                bool hasAccess = _objMadicalScrutinyVM
                                    .GetAdjudicatorLetterAccess(ClaimID, SlNo);

                return Json(hasAccess, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName =
                    System.Web.Configuration.WebConfigurationManager
                    .AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));

                return Json(false, JsonRequestBehavior.AllowGet);
            }
        }

        [Authorize]
        [HttpPost]
        public string Save_ClaimRejectedReasons(string ClaimID, string SlNo, string ClaimRejections, string ClaimTypeID, string RequestTypeID, string PolicyTypeID,
            string ServiceTypeID, string ServiceSubTypeID, string ClaimedAmount, string MainMemberPolicyID, string PolicyID, string ProviderID, string BrokerID,
            string PayerID, string CorporateID, string InsuranceCompanyID, string ClaimRules, int AgentID, string DeductibleAmt, string PremiumAmount, string QMSID, string QMSAdminID)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    CommonController _objCommon = new CommonController();
                    int ClaimsStageID = Convert.ToInt32(Resources.StageIDs.Rejection);//23

                    DataTable dtClaimRejections = GetClaimRejectionTableStructure(ClaimRejections);
                    //DataTable dtClaimRejections = (DataTable)JsonConvert.DeserializeObject(ClaimRejections, (typeof(DataTable)));
                    DataTable dtClaimRules = (DataTable)JsonConvert.DeserializeObject(ClaimRules, (typeof(DataTable)));

                    string vMessage = string.Empty;
                    DataSet dsResult = null;
                    if (CorporateID == "")
                        CorporateID = "0";
                    dsResult = _objMadicalScrutinyVM.Save_ClaimRejectedReasonsVM(Convert.ToInt64(ClaimID), Convert.ToInt16(SlNo), dtClaimRejections, Convert.ToInt32(ClaimTypeID),
                       Convert.ToInt32(RequestTypeID), Convert.ToInt16(PolicyTypeID), Convert.ToInt32(ServiceTypeID), Convert.ToInt32(ServiceSubTypeID), ClaimsStageID, ClaimRejectionRoleID,
                       Convert.ToInt32(Session[Resources.SessionValue.RegionID]), Convert.ToDecimal(ClaimedAmount), Convert.ToDecimal(DeductibleAmt),
                       Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]), dtClaimRules.Rows.Count == 0 ? null : dtClaimRules, Convert.ToDecimal(PremiumAmount), out vMessage, Convert.ToInt32(CorporateID));

                    if (dsResult.Tables.Count != 0)
                    {
                        if ((Convert.ToInt32(RequestTypeID) == 1 || Convert.ToInt32(RequestTypeID) == 2 || Convert.ToInt32(RequestTypeID) == 3) && dsResult.Tables[1].Rows.Count > 0)
                            vMessage = vMessage + " ; " + Save_ePreauthDetails(23, Convert.ToInt64(ClaimID), Convert.ToInt32(SlNo));
                        if (dsResult.Tables[0].Rows.Count > 0 && dsResult.Tables[1].Rows.Count > 0 && dsResult.Tables[2].Rows.Count > 0)
                        {
                            //var controllerB = new CommonController();
                            //controllerB.InitializeController(this.Request.RequestContext);
                            ////CommunicatingRejectedReasons(ref dsResult, Convert.ToInt64(ClaimID), Convert.ToByte(SlNo), Convert.ToInt64(MainMemberPolicyID), Convert.ToInt64(PolicyID), Convert.ToInt64(ProviderID), Convert.ToInt32(BrokerID), Convert.ToInt64(CorporateID), Convert.ToInt64(PayerID), Convert.ToInt32(InsuranceCompanyID));
                            _objCommon.CommunicationInsert_Common(ref dsResult, Convert.ToInt64(ClaimID), Convert.ToInt32(SlNo), Convert.ToInt64(MainMemberPolicyID),
                                Convert.ToInt64(PolicyID), Convert.ToInt64(ProviderID), Convert.ToInt32(BrokerID == "" ? "0" : BrokerID), Convert.ToInt64(CorporateID == "" ? "0" : CorporateID),
                                Convert.ToInt64(PayerID), Convert.ToInt32(InsuranceCompanyID), ClaimsStageID, "MedicalScrutinyController", Convert.ToInt32(Session[SessionValue.UserRegionID]), 0, AgentID);
                            if (dsResult.Tables[3].Rows.Count > 0)
                            {
                                DataSet dsResultforapproval = _objMadicalScrutinyVM.Get_Approvalletterdata(Convert.ToInt64(dsResult.Tables[3].Rows[0]["ClaimID"]), Convert.ToInt16(dsResult.Tables[3].Rows[0]["Slno"]), 1, 3, Convert.ToInt16(dsResult.Tables[3].Rows[0]["RequestTypeID"]));
                                if (dsResultforapproval.Tables.Count != 0)
                                {
                                    if (dsResultforapproval.Tables[0].Rows.Count > 0 && dsResultforapproval.Tables[1].Rows.Count > 0)
                                    {
                                        _objCommon.CommunicationInsert_Common(ref dsResultforapproval, Convert.ToInt64(dsResult.Tables[3].Rows[0]["ClaimID"]), Convert.ToInt16(dsResult.Tables[3].Rows[0]["Slno"]), Convert.ToInt64(dsResult.Tables[3].Rows[0]["MainMemberPolicyID"]),
                                         Convert.ToInt64(dsResult.Tables[3].Rows[0]["PolicyID"]), Convert.ToInt64(dsResult.Tables[3].Rows[0]["ProviderID"]), Convert.ToInt32(dsResult.Tables[3].Rows[0]["BrokerID"].ToString() == "" ? "0" : dsResult.Tables[3].Rows[0]["BrokerID"]), Convert.ToInt64(dsResult.Tables[3].Rows[0]["CorporateID"].ToString() == "" ? "0" : dsResult.Tables[3].Rows[0]["CorporateID"]),
                                         Convert.ToInt64(dsResult.Tables[3].Rows[0]["PayerID"]), Convert.ToInt32(dsResult.Tables[3].Rows[0]["InsuranceCompanyID"]), 24, "MedicalScrutinyController", Convert.ToInt32(Session[SessionValue.UserRegionID]), 0, Convert.ToInt32(dsResult.Tables[3].Rows[0]["AgentID"].ToString() == "" ? "0" : dsResult.Tables[3].Rows[0]["AgentID"]));
                                    }
                                }
                            }
                        }
                    }

                    string QMS = string.Empty;
                    QMS = QMSID;
                    string QMSadmin = string.Empty;
                    QMSadmin = QMSAdminID;

                    Qmsv2CMController qms = new Qmsv2CMController();
                    qms.UpdateClaimStatus("UPDATESTATUS", "", "", "", "", QMS, "5", Session["UserRegionID"].ToString());

                    Task<string> task;
                    if (Convert.ToInt32(RequestTypeID) == 1 || Convert.ToInt32(RequestTypeID) == 2 || Convert.ToInt32(RequestTypeID) == 3)
                    {
                        DataTable dt = _objMadicalScrutinyVM.getcorlidfromdb(Convert.ToInt64(ClaimID), Convert.ToInt16(SlNo), 1);
                        if (dt.Rows.Count > 0)
                        {
                            if (dt.Rows[0]["RequestType"].ToString() == "6")
                                task = _objMadicalScrutinyVM.PreauthOnSubmitBundle(dt.Rows[0]["CorelationId"].ToString(), 3);
                            else
                                task = _objMadicalScrutinyVM.PreauthOnSubmitBundle(dt.Rows[0]["CorelationId"].ToString(), 1);
                        }
                    }
                    else if (Convert.ToInt32(RequestTypeID) == 4 && Convert.ToInt32(ClaimTypeID) == 1)
                    {
                        DataTable dt = _objMadicalScrutinyVM.getcorlidfromdb(Convert.ToInt64(ClaimID), Convert.ToInt16(SlNo), 4);
                        if (dt.Rows.Count > 0)
                        {
                            if (dt.Rows[0]["RequestType"].ToString() == "6")
                                task = _objMadicalScrutinyVM.PreauthOnSubmitBundle(dt.Rows[0]["CorelationId"].ToString(), 3);
                            else
                                task = _objMadicalScrutinyVM.PreauthOnSubmitBundle(dt.Rows[0]["CorelationId"].ToString(), 2);
                        }
                    }

                    return vMessage;
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "Save_QueryPendingDetails", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        /// <summary>
        /// Copy the data from Deserialized datatable to claimRejection table having the same structure of user defined table type dbo.ClaimRejections 
        /// Abhishek 26 Oct 23
        /// </summary>
        /// <param name="ClaimRejections"></param>
        /// <returns></returns>
        private static DataTable GetClaimRejectionTableStructure(string ClaimRejections)
        {
            DataTable tempTable = (DataTable)JsonConvert.DeserializeObject(ClaimRejections, (typeof(DataTable)));

            //Creating data table instance
            DataTable claimRejection = new DataTable("claimRejection");
            //Add the DataColumn using all properties
            DataColumn RejectionReasonsID = new DataColumn("RejectionReasonsID")
            {
                DataType = typeof(int),
                AllowDBNull = true
            };
            claimRejection.Columns.Add(RejectionReasonsID);

            //Add the DataColumn few properties
            DataColumn FreeText1 = new DataColumn("FreeText1")
            {
                MaxLength = 500,
                AllowDBNull = true
            };
            claimRejection.Columns.Add(FreeText1);

            DataColumn FreeText2 = new DataColumn("FreeText2")
            {
                MaxLength = 500,
                AllowDBNull = true
            };
            claimRejection.Columns.Add(FreeText2);

            //Add the DataColumn using defaults
            DataColumn RejectionCategory = new DataColumn("RejectionCategory")
            {
                DataType = typeof(int),
                AllowDBNull = true
            };
            claimRejection.Columns.Add(RejectionCategory);

            DataColumn RejectionSubCategory = new DataColumn("RejectionSubCategory")
            {
                DataType = typeof(int),
                AllowDBNull = true
            };
            claimRejection.Columns.Add(RejectionSubCategory);

            DataColumn Remarks = new DataColumn("Remarks")
            {
                MaxLength = 2500,
                AllowDBNull = true
            };
            claimRejection.Columns.Add(Remarks);

            DataColumn InsurerRejectionID = new DataColumn("InsurerRejectionID")
            {
                DataType = typeof(int),
                AllowDBNull = true
            };
            claimRejection.Columns.Add(InsurerRejectionID);

            if (tempTable != null)
            {
                if (tempTable.Rows.Count > 0)
                {
                    foreach (DataRow row in tempTable.Rows)
                    {
                        claimRejection.Rows.Add(
                            tempTable.Columns.Contains("RejectionReasonsID") ? row["RejectionReasonsID"] : DBNull.Value,
                            tempTable.Columns.Contains("FreeText1") ? row["FreeText1"] : DBNull.Value,
                            tempTable.Columns.Contains("FreeText2") ? row["FreeText2"] : DBNull.Value,
                            tempTable.Columns.Contains("RejectionCategory") ? row["RejectionCategory"] : DBNull.Value,
                            tempTable.Columns.Contains("RejectionSubCategory") ? row["RejectionSubCategory"] : DBNull.Value,
                            tempTable.Columns.Contains("Remarks") ? row["Remarks"] : DBNull.Value,
                            tempTable.Columns.Contains("InsurerRejectionID") ? row["InsurerRejectionID"] : DBNull.Value
                             );
                    }
                }
            }

            return claimRejection;
        }

        [Authorize]
        // Billing Calculations By Srinu B Start
        public string BillingCalcDetails_Retrieve(long ClaimID, int SlNo, int claimstageid, string caltype)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.BillingCalcDetails_Retrieve(ClaimID, SlNo, claimstageid, caltype));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "BillingCalcDetails_Retrieve", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }
        // Billing Calculations By Srinu B End

        [Authorize]
        public string ReferInsDetails_Retrieve(long ClaimID, int SlNo)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.ReferInsDetails_Retrieve(ClaimID, SlNo));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "ReferInsDetails_Retrieve", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        [Authorize]
        public string ResponsefromInsDetails_Retrieve(long ClaimID, int SlNo)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.ResponsefromInsDetails_Retrieve(ClaimID, SlNo));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "ResponsefromInsDetails_Retrieve", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        [Authorize]
        public string AuditRemarksDetails_Retrieve(long ClaimID, int SlNo)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.AuditRemarksDetails_Retrieve(ClaimID, SlNo));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "AuditRemarksDetails_Retrieve", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        [Authorize]
        /* Save Settlement Details */
        public string Save_SettlementDetails(string ClaimDetails, string PolicyType, string MainMemberPolicyID, string PolicyID, string ProviderID,
            string BrokerID, string PayerID, string CorporateID, string InsuranceCompanyID, int AgentID)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    int ClaimsStageID = Convert.ToInt32(Resources.StageIDs.Settlement);//27

                    Newtonsoft.Json.Linq.JObject JObject = Newtonsoft.Json.Linq.JObject.Parse(ClaimDetails);
                    ClaimDetails objClaimDetails = new ClaimDetails();
                    objClaimDetails.ClaimID = Convert.ToInt64(JObject["ClaimID"]);
                    objClaimDetails.Slno = Convert.ToInt16(JObject["SlNo"]);
                    objClaimDetails.SettledAmount = Convert.ToDecimal(JObject["SettledAmount"]);
                    objClaimDetails.ModeOfPaymentID = Convert.ToInt32(JObject["ModeOfPaymentID"]);
                    objClaimDetails.BankTransactionNo = JObject["BankTransactionNo"].ToString();
                    objClaimDetails.ChequeDate = Convert.ToDateTime(JObject["ChequeDate"]);
                    objClaimDetails.BankAccountNo = JObject["BankAccountNo"].ToString();
                    objClaimDetails.BankName = JObject["BankName"].ToString();
                    objClaimDetails.IFSCCode = JObject["IFSCCode"].ToString();

                    string vMessage = string.Empty;
                    //int id = _objMadicalScrutinyVM.Save_SettlementDetailsVM(objClaimDetails, ClaimSettlementRoleID,
                    //    Convert.ToInt32(Session[Resources.SessionValue.RegionID]), ClaimsStageID, Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]), out vMessage);

                    DataSet dsResult = null;
                    dsResult = _objMadicalScrutinyVM.Save_SettlementDetailsVM(objClaimDetails, ClaimSettlementRoleID,
                      Convert.ToInt32(Session[Resources.SessionValue.RegionID]), ClaimsStageID, Convert.ToInt16(PolicyType), Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]), Convert.ToInt64(PayerID), Convert.ToInt32(CorporateID), out vMessage);

                    if (dsResult.Tables.Count != 0)
                    {
                        if (dsResult.Tables[0].Rows.Count > 0 && dsResult.Tables[1].Rows.Count > 0)//&& dsResult.Tables[2].Rows.Count > 0)
                        {
                            ////CommunicatingQuerypending(ref dsResult, Convert.ToInt64(ClaimID), Convert.ToByte(SlNo), Convert.ToInt64(MainMemberPolicyID), Convert.ToInt64(PolicyID), Convert.ToInt64(ProviderID), Convert.ToInt32(BrokerID), Convert.ToInt64(CorporateID), Convert.ToInt64(PayerID), Convert.ToInt32(InsuranceCompanyID));

                            _objCommon.CommunicationInsert_Common(ref dsResult, Convert.ToInt64(JObject["ClaimID"]), Convert.ToInt32(JObject["SlNo"]), Convert.ToInt64(MainMemberPolicyID),
                                Convert.ToInt64(PolicyID), Convert.ToInt64(ProviderID), Convert.ToInt32(BrokerID), Convert.ToInt64(CorporateID),
                                Convert.ToInt64(PayerID), Convert.ToInt32(InsuranceCompanyID), ClaimsStageID, "MedicalScrutinyController", Convert.ToInt32(Session[SessionValue.UserRegionID]), 0, AgentID);
                        }
                    }

                    new DefaultCacheProvider().Invalidate(Convert.ToString(JObject["ClaimID"]));
                    return vMessage;
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutiny", "Save_SettlementDetails", Session[SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }

        }

        [Authorize]
        public string ClaimCommunication_Resend(long ID, long ClaimID, int SlNo, string SentTo, string SentCC, string SentBCC)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    new CommonViewModel().CommunicationResendTransactionInsert(ID, SentTo, SentCC, SentBCC, Convert.ToInt32(Session[SessionValue.UserRegionID]));

                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.ClaimCommunication_Retrieve(ClaimID, SlNo));
                    //return "Communication Resend successfully";
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "ClaimCommunication_Retrieve", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }

        }

        [Authorize]
        public string CRMRemarksDetails_Retrieve(long ClaimID, int SlNo)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.CRMRemarksDetails_Retrieve(ClaimID, SlNo));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "AuditRemarksDetails_Retrieve", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        [Authorize]
        public string InvestigationRemarksDetails_Retrieve(long ClaimID, int SlNo)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.InvestigationRemarksDetails_Retrieve(ClaimID, SlNo));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "AuditRemarksDetails_Retrieve", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }
        [Authorize]
        public string IsReassigninvestigation(long ClaimID, int SlNo)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.IsReassigninvestigation(ClaimID, SlNo));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "AuditRemarksDetails_Retrieve", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }



        [Authorize]
        public string InvestigationFeedBackRemarksDetails_Retrieve(long ClaimID, int SlNo)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.InvestigationFeedBackRemarksDetails_Retrieve(ClaimID, SlNo));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "AuditRemarksDetails_Retrieve", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }
        [Authorize]
        public string InvestigationFeedBackBimaDropdownDetails_Retrieve()
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.InvestigationFeedBackBimaDropdownDetails_Retrieve());
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "AuditRemarksDetails_Retrieve", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        [Authorize]
        public string BillViewRetrieve(long ClaimID, int SlNo)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.BillView_Retrieve(ClaimID, SlNo));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "BillViewRetrieve", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        [Authorize]
        public string IRClose_Insert(string ClaimDetails)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    IRCloseDetails objCloseDetails = new IRCloseDetails();
                    string msg = string.Empty;
                    Newtonsoft.Json.Linq.JObject JObject = Newtonsoft.Json.Linq.JObject.Parse(ClaimDetails);

                    objCloseDetails.ClaimID = Convert.ToInt64(JObject["ClaimID"]);
                    objCloseDetails.Slno = Convert.ToInt16(JObject["Slno"]);
                    objCloseDetails.Remarks = Convert.ToString(JObject["Remarks"]);
                    objCloseDetails.ClaimTypeID = Convert.ToInt16(JObject["ClaimTypeID"]);
                    objCloseDetails.RequestTypeID = Convert.ToInt16(JObject["RequestTypeID"]);
                    objCloseDetails.ServiceTypeID = Convert.ToInt16(JObject["ServiceTypeID"]);
                    objCloseDetails.ServiceSubTypeID = Convert.ToInt16(JObject["ServiceSubTypeID"]);
                    objCloseDetails.RegionID = Convert.ToInt32(Session[Resources.SessionValue.RegionID]);
                    objCloseDetails.ClaimedAmount = Convert.ToDecimal(JObject["ClaimedAmount"]);
                    objCloseDetails.PolicyType = Convert.ToInt16(JObject["PolicyType"]);
                    objCloseDetails.IssueID = Convert.ToInt16(JObject["IssueID"]);
                    objCloseDetails.CreatedUserRegionID = Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]);

                    DataSet dsResult = null;
                    dsResult = _objMadicalScrutinyVM.IRClose_InsertVM(objCloseDetails, out msg);

                    if (dsResult.Tables.Count != 0)
                    {
                        if (dsResult.Tables[0].Rows.Count > 0 && dsResult.Tables[1].Rows.Count > 0)
                        {
                            _objCommon.CommunicationInsert_Common(ref dsResult, Convert.ToInt64(JObject["ClaimID"]), Convert.ToInt32(JObject["Slno"]), Convert.ToInt64(JObject["MainMemberPolicyID"]),
                                 Convert.ToInt64(JObject["PolicyID"]), Convert.ToInt64(JObject["ProviderID"]), Convert.ToInt32(JObject["BrokerID"]), Convert.ToInt64(JObject["CorporateID"]),
                                 Convert.ToInt64(JObject["PayerID"]), Convert.ToInt32(JObject["IssueID"]), Convert.ToInt32(JObject["ClaimsStageID"]), "MedicalScrutinyController", Convert.ToInt32(Session[SessionValue.UserRegionID]), 0, Convert.ToInt16(JObject["AgentID"]));
                        }
                    }

                    return Newtonsoft.Json.JsonConvert.SerializeObject(msg);
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "Adjudication_Actions_Insert", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        [Authorize]
        public string ClaimInformationSheet_Retrieve(long ClaimID, Int16 SlNo)
        {
            try
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.ClaimInformationSheet_RetrieveVM(ClaimID, SlNo));
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        [Authorize]
        public string SettlementDetails_Retrieve(long ClaimID, Int16 SlNo, bool IsFrmArchived = false)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.SettlementDetails_RetrieveVM(ClaimID, SlNo, IsFrmArchived));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "AuditRemarksDetails_Retrieve", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));

                ReturnError rtnObj = new ReturnError();
                rtnObj.ID = 1;
                rtnObj.Message = ex.Message;
                return Newtonsoft.Json.JsonConvert.SerializeObject(rtnObj);

            }
        }
        [Authorize]
        public string Check_OpenActionItems(long ClaimID, int slno)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.Check_OpenActionItemsVM(ClaimID, slno));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "AuditRemarksDetails_Retrieve", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));

                ReturnError rtnObj = new ReturnError();
                rtnObj.ID = 1111;
                rtnObj.Message = ex.Message;
                return Newtonsoft.Json.JsonConvert.SerializeObject(rtnObj);

            }
        }

        [Authorize]
        public string SaveClaimConsignmentDetails(string ClaimID, string SlNo, string ConsignmentNo, string ConsignmentDate, string CourierID, string DocumentTypeID, int ClaimStageID, int FreightModeID, int DeliveryStatusID)
        {
            try
            {
                if (SessionValue.LoginUserID != "" && Session[SessionValue.UserRegionID] != null)
                {
                    int UserID = Convert.ToInt32(Session[SessionValue.LoginUserID]);
                    int DispatchedBranch = Convert.ToInt32(Session[SessionValue.UserRegionID]);

                    int id = _objMadicalScrutinyVM.SaveClaimConsignmentDetails(Convert.ToInt64(ClaimID), Convert.ToInt16(SlNo), ConsignmentNo,
                        Convert.ToDateTime(ConsignmentDate), Convert.ToInt32(CourierID), DispatchedBranch, Convert.ToInt16(DocumentTypeID), UserID,
                        ClaimStageID, FreightModeID, DeliveryStatusID);

                    return id.ToString();
                }
                else
                {
                    return "0";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "ClaimsController", "Save_BalanceSumInsured", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return "0";
            }
        }

        [Authorize]
        public string GetClaimConsignmentDetails(Int64 ClaimeID, bool IsFrmArchived = false)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.GetClaimConsignmentDetails(ClaimeID, IsFrmArchived));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                ReturnError rtnObj = new ReturnError();
                rtnObj.ID = 1;
                rtnObj.Message = ex.Message;
                return Newtonsoft.Json.JsonConvert.SerializeObject(rtnObj);
            }
        }

        /*Preauth Cancel*/
        [Authorize]
        public string PreauthCancel(string ClaimID, string SlNo, string PreauthCancelRemarks, string ClaimTypeID, string RequestTypeID, string PolicyTypeID,
          string ServiceTypeID, string ServiceSubTypeID, string ClaimedAmount, string MainMemberPolicyID, string PolicyID, string ProviderID, string BrokerID,
           string PayerID, string CorporateID, string InsuranceCompanyID, int AgentID, string QMSID, string QMSAdminID)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    //SP3V-3120 - SP3V-3079-- Leena If Preauth enh and preauth final found do not cancel sr no 1 show alert msg
                    if (ClaimTypeID == "1")
                    {
                        int CntEnhFinalRequest = _objMadicalScrutinyVM.GetClaimEnhanceFinalRequest(Convert.ToInt64(ClaimID), RequestTypeID);
                        if (CntEnhFinalRequest > 0)
                        {
                            return Newtonsoft.Json.JsonConvert.SerializeObject("Please cancel the latest preauth!");
                        }
                    }
                    //SP3V-3120 - SP3V-3079 End
                    CommonController _objCommon = new CommonController();
                    int ClaimsStageID = Convert.ToInt32(Resources.StageIDs.Cancel);//21


                    string vMessage = string.Empty;
                    DataSet dsResult = null;
                    dsResult = _objMadicalScrutinyVM.PreauthCancelVM(Convert.ToInt64(ClaimID), Convert.ToInt16(SlNo), PreauthCancelRemarks, Convert.ToInt32(ClaimTypeID),
                       Convert.ToInt32(RequestTypeID), Convert.ToInt16(PolicyTypeID), Convert.ToInt32(ServiceTypeID), Convert.ToInt32(ServiceSubTypeID), ClaimsStageID, ClaimRejectionRoleID,
                       Convert.ToInt32(Session[Resources.SessionValue.RegionID]), Convert.ToDecimal(ClaimedAmount),
                       Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]), out vMessage);

                    if (dsResult.Tables.Count != 0)
                    {
                        if ((Convert.ToInt32(RequestTypeID) == 1 || Convert.ToInt32(RequestTypeID) == 2 || Convert.ToInt32(RequestTypeID) == 3) && dsResult.Tables[1].Rows.Count > 0)
                            vMessage = vMessage + " ; " + Save_ePreauthDetails(21, Convert.ToInt64(ClaimID), Convert.ToInt32(SlNo));
                        if (dsResult.Tables[0].Rows.Count > 0 && dsResult.Tables[1].Rows.Count > 0 && dsResult.Tables[2].Rows.Count > 0)
                        {
                            _objCommon.CommunicationInsert_Common(ref dsResult, Convert.ToInt64(ClaimID), Convert.ToInt32(SlNo), Convert.ToInt64(MainMemberPolicyID),
                                Convert.ToInt64(PolicyID), Convert.ToInt64(ProviderID), Convert.ToInt32(BrokerID), Convert.ToInt64(CorporateID == "" ? "0" : CorporateID),
                                Convert.ToInt64(PayerID), Convert.ToInt32(InsuranceCompanyID), ClaimsStageID, "MedicalScrutinyController", Convert.ToInt32(Session[SessionValue.UserRegionID]), 0, AgentID);
                        }
                    }

                    string QMS = string.Empty;
                    QMS = QMSID;
                    string QMSadmin = string.Empty;
                    QMSadmin = QMSAdminID;

                    Qmsv2CMController qms = new Qmsv2CMController();
                    qms.UpdateClaimStatus("UPDATESTATUS", "", "", "", "", QMS, "5", Session["UserRegionID"].ToString());

                    Task<string> task;
                    if (Convert.ToInt32(RequestTypeID) == 1 || Convert.ToInt32(RequestTypeID) == 2 || Convert.ToInt32(RequestTypeID) == 3)
                    {
                        DataTable dt = _objMadicalScrutinyVM.getcorlidfromdb(Convert.ToInt64(ClaimID), Convert.ToInt32(SlNo), 1);
                        if (dt.Rows.Count > 0)
                        {
                            if (dt.Rows[0]["RequestType"].ToString() == "6")
                                task = _objMadicalScrutinyVM.PreauthOnSubmitBundle(dt.Rows[0]["CorelationId"].ToString(), 3);
                            else
                                task = _objMadicalScrutinyVM.PreauthOnSubmitBundle(dt.Rows[0]["CorelationId"].ToString(), 1);
                        }
                    }
                    else if (Convert.ToInt32(RequestTypeID) == 4)
                    {
                        DataTable dt = _objMadicalScrutinyVM.getcorlidfromdb(Convert.ToInt64(ClaimID), Convert.ToInt32(SlNo), 4);
                        if (dt.Rows.Count > 0)
                        {
                            if (dt.Rows[0]["RequestType"].ToString() == "6")
                                task = _objMadicalScrutinyVM.PreauthOnSubmitBundle(dt.Rows[0]["CorelationId"].ToString(), 3);
                            else
                                task = _objMadicalScrutinyVM.PreauthOnSubmitBundle(dt.Rows[0]["CorelationId"].ToString(), 2);
                        }
                    }


                    return Newtonsoft.Json.JsonConvert.SerializeObject(vMessage);
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "Save_QueryPendingDetails", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                ReturnError rtnObj = new ReturnError();
                rtnObj.ID = 1;
                rtnObj.Message = ex.Message;
                return Newtonsoft.Json.JsonConvert.SerializeObject(rtnObj);
            }
        }

        public string ClaimCencel_Reasons_Retrieve(long ClaimID, int SlNo, long ActionID, int StageID)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.ClaimCencel_Reasons_RetrieveVM(ClaimID, SlNo, ActionID, StageID));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "ClaimAudit_Retrieve", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));

                ReturnError rtnObj = new ReturnError();
                rtnObj.ID = 1;
                rtnObj.Message = "Error while getting reasons.";
                return Newtonsoft.Json.JsonConvert.SerializeObject(rtnObj);

                //return ex.Message;
            }
        }


        //public string OpenDMSDocument()
        //{
        //    try
        //    {
        //        if (Session[SessionValue.UserRegionID] != null)
        //        {
        //            string DMSClaimid="376343";
        //            NameValueCollection data = new NameValueCollection();
        //            data.Add("v1", DMSClaimid);
        //            //var url = "http://119.226.90.163:8081/webdesktop/URLIntegration/DocIntegration2.jsp?Claimid=" + DMSClaimid + "";
        //            var url = "http://192.168.70.66:8081/webdesktop/URLIntegration/DocIntegration2.jsp?Claimid=" + DMSClaimid + "";
        //            HttpHelper.RedirectAndPOST("../View/Common/WebForm1.aspx", url, data);

        //            return "";
        //        }
        //        else
        //        {
        //            return "ErrorCode#1";
        //        }               
        //    }
        //    catch (Exception ex)
        //    {
        //        _objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "BillViewRetrieve", Session[Resources.SessionValue.LoginUserID].ToString());
        //        return "ErrorCode#1";
        //    }
        //}

        /* Start Communication*/
        //public void CommunicationInsert_Common(ref DataSet Communicationdata, long? ClaimID, Byte SlNo, long MemberPolicyID, long? PolicyID, long ProviderID, int? BrokerID, long? CorpID, long? PayerID, int? IssueID, string MailType)
        //{
        //    try
        //    {
        //        CommonController _objCommon = new CommonController();
        //        CommonViewModel _objCommonVM = new CommonViewModel();

        //        DataTable dt = null;
        //        dt = _objCommon.GetEmailCommunicationDetails(Communicationdata.Tables[0].Rows[0]["Entity_To"].ToString(),
        //             Communicationdata.Tables[0].Rows[0]["Entity_CC"].ToString(), Communicationdata.Tables[0].Rows[0]["Entity_BCC"].ToString(), IssueID, CorpID,
        //             PayerID, PolicyID, ProviderID, MemberPolicyID, ClaimID, BrokerID, Communicationdata.Tables[0].Rows[0]["SMS_To"].ToString());

        //        DataRow row = dt.NewRow();
        //        row["Email"] = Convert.ToString(Communicationdata.Tables[0].Rows[0]["Email_To"]);
        //        row["Email_CC"] = Convert.ToString(Communicationdata.Tables[0].Rows[0]["Email_cc"]);
        //        row["Email_BCC"] = Convert.ToString(Communicationdata.Tables[0].Rows[0]["Email_BCC"]);
        //        dt.Rows.Add(row);

        //        string ToEmail, CCEmail, BCCEmail; ToEmail = CCEmail = BCCEmail = "";
        //        for (int i = 0; i < dt.Rows.Count; i++)
        //        {
        //            if (dt.Rows[i]["Email"].ToString() != "") ToEmail += "," + dt.Rows[i]["Email"];
        //            if (dt.Rows[i]["Email_CC"].ToString() != "") CCEmail += "," + dt.Rows[i]["Email_CC"];
        //            if (dt.Rows[i]["Email_BCC"].ToString() != "") BCCEmail += "," + dt.Rows[i]["Email_BCC"];
        //        }

        //        ////string FromMailID = ConfigurationManager.AppSettings["FromMail"].ToString();
        //        string FromMailID = Convert.ToString(Communicationdata.Tables[0].Rows[0]["email_from"]);
        //        string EmailBody = string.Empty;
        //        string EmailSubject = string.Empty;
        //        _objCommon.FormatHtmlTemplate(Communicationdata.Tables[1], Communicationdata.Tables[0].Rows[0]["Email_Body"].ToString(), out EmailBody);
        //        _objCommon.FormatHtmlTemplate(Communicationdata.Tables[1], Communicationdata.Tables[0].Rows[0]["Email_Subject"].ToString(), out EmailSubject);

        //        string vReasons = "";
        //        if (MailType.ToUpper() == "R")//Rejected
        //        {
        //            vReasons = vReasons + "<br><table style='border: thin Solid #CCCCCC; LINE-HEIGHT: 25px; FONT-SIZE:13px; FONT-FAMILY: verdana'> <tr> <td style='border: thin Solid #CCCCCC;width:60px;'><b> Sl No</b> </td> <td style='border: thin Solid #CCCCCC;'><b>Rejected Reasons </b> </td>  </tr>";
        //            for (int i = 0; i < Communicationdata.Tables[2].Rows.Count; i+l;+)
        //            {
        //                vReasons = vReasons + " <tr> <td style='border: thin Solid #CCCCCC;'> " + (i + 1) + " </td> <td style='border: thin Solid #CCCCCC;'> " + Communicationdata.Tables[2].Rows[i]["Remarks"].ToString() + " </td>  </tr>";
        //            }
        //            vReasons = vReasons + "</table><br>";
        //            EmailBody = EmailBody.Replace("&lt;&lt;" + "Remarks" + "&gt;&gt;", vReasons);
        //        }
        //        else if (MailType.ToUpper() == "QP")//Query Pending
        //        {
        //            vReasons = vReasons + "<br><table style='border: thin Solid #CCCCCC; LINE-HEIGHT: 25px; FONT-SIZE:13px; FONT-FAMILY: verdana'> <tr> <td style='border: thin Solid #CCCCCC;width:60px;'><b> Sl No</b> </td> <td style='border: thin Solid #CCCCCC;'><b> Particulars of Details / Documents Required</b> </td> <td style='border: thin Solid #CCCCCC;width:60px;'><b> Amount </b></td> <td style='border: thin Solid #CCCCCC;width:60px;'> <b>Status</b></td> <td style='border: thin Solid #CCCCCC;width:140px;'> <b>Requirement Type</b> </td> </tr>";
        //            for (int i = 0; i < Communicationdata.Tables[2].Rows.Count; i++)
        //            {
        //                vReasons = vReasons + " <tr> <td style='border: thin Solid #CCCCCC;'> " + (i + 1) + " </td> <td style='border: thin Solid #CCCCCC;'> " + Communicationdata.Tables[2].Rows[i]["IRReason"].ToString() + " </td> <td style='border: thin Solid #CCCCCC;'> " + Communicationdata.Tables[2].Rows[i]["Amount"].ToString() + " </td> <td style='border: thin Solid #CCCCCC;'> " + Communicationdata.Tables[2].Rows[i]["RequirementType"].ToString() + " </td> <td style='border: thin Solid #CCCCCC;'> " + Communicationdata.Tables[2].Rows[i]["isReceived"].ToString() + " </td> </tr>";
        //            }
        //            vReasons = vReasons + "</table><br>";
        //            EmailBody = EmailBody.Replace("&lt;&lt;" + "QueryRemarks" + "&gt;&gt;", vReasons);
        //        }

        //        //_objCommon.CommonForSendingMail(EmailSubject, FromMailID, ToEmail, CCEmail, BCCEmail, EmailBody, false);

        //        int UserregionId = Convert.ToInt32(Session[SessionValue.UserRegionID]);
        //        _objCommonVM.CommunicationTransactionInsert(ClaimID, SlNo, StageId, 86, FromMailID, ToEmail, CCEmail, BCCEmail, EmailSubject, EmailBody, Convert.ToInt32(Communicationdata.Tables[0].Rows[0]["AttributeID"].ToString()), DateTime.Now, UserregionId, false);
        //    }
        //    catch (Exception ex)
        //    {
        //        if (MailType.ToUpper() == "R")//Rejected
        //            new CommonController().ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "CommunicatingRejectedReasons", Session[Resources.SessionValue.LoginUserID].ToString());
        //        else if (MailType.ToUpper() == "QP")//Query Pending
        //            new CommonController().ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "CommunicatingRejectedReasons", Session[Resources.SessionValue.LoginUserID].ToString());
        //        else
        //            new CommonController().ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "CommunicationSettled", Session[Resources.SessionValue.LoginUserID].ToString());
        //        throw ex;
        //    }
        //}


        //public void CommunicatingQuerypending(ref DataSet Communicationdata, long? ClaimID, Byte SlNo, long MemberPolicyID, long? PolicyID, long ProviderID, int? BrokerID, long? CorpID, long? PayerID, int? IssueID)
        //{
        //    try
        //    {
        //        CommonController _objCommon = new CommonController();
        //        DataTable dt = null;
        //        dt = _objCommon.GetEmailCommunicationDetails(Communicationdata.Tables[0].Rows[0]["Entity_To"].ToString(), Communicationdata.Tables[0].Rows[0]["Entity_CC"].ToString(), Communicationdata.Tables[0].Rows[0]["Entity_BCC"].ToString(), IssueID, CorpID, PayerID, PolicyID, ProviderID, MemberPolicyID, ClaimID, BrokerID);

        //        DataRow row = dt.NewRow();

        //        row["Email"] = Convert.ToString(Communicationdata.Tables[0].Rows[0]["Email_To"]);
        //        row["Email_CC"] = Convert.ToString(Communicationdata.Tables[0].Rows[0]["Email_cc"]);
        //        row["Email_BCC"] = Convert.ToString(Communicationdata.Tables[0].Rows[0]["Email_BCC"]);
        //        dt.Rows.Add(row);

        //        if (dt.Rows.Count > 0)
        //        {
        //            string ToEmail, CCEmail, BCCEmail; ToEmail = CCEmail = BCCEmail = "";
        //            for (int i = 0; i < dt.Rows.Count; i++)
        //            {
        //                if (dt.Rows[i]["Email"].ToString() != "") ToEmail += "," + dt.Rows[i]["Email"];
        //                if (dt.Rows[i]["Email_CC"].ToString() != "") CCEmail += "," + dt.Rows[i]["Email_CC"];
        //                if (dt.Rows[i]["Email_BCC"].ToString() != "") BCCEmail += "," + dt.Rows[i]["Email_BCC"];
        //            }
        //            string FromMailID = Convert.ToString(Communicationdata.Tables[0].Rows[0]["email_from"]);// ConfigurationManager.AppSettings["FromMail"].ToString();
        //            string EmailBody = string.Empty;
        //            string EmailSubject = string.Empty;
        //            _objCommon.FormatHtmlTemplate(Communicationdata.Tables[1], Communicationdata.Tables[0].Rows[0]["Email_Body"].ToString(), out EmailBody);
        //            _objCommon.FormatHtmlTemplate(Communicationdata.Tables[1], Communicationdata.Tables[0].Rows[0]["Email_Subject"].ToString(), out EmailSubject);

        //            string vReasons = "";
        //            vReasons = vReasons + "<br><table style='border: thin Solid #CCCCCC; LINE-HEIGHT: 25px; FONT-SIZE:13px; FONT-FAMILY: verdana'> <tr> <td style='border: thin Solid #CCCCCC;width:60px;'><b> Sl No</b> </td> <td style='border: thin Solid #CCCCCC;'><b> Particulars of Details / Documents Required</b> </td> <td style='border: thin Solid #CCCCCC;width:60px;'><b> Amount </b></td> <td style='border: thin Solid #CCCCCC;width:60px;'> <b>Status</b></td> <td style='border: thin Solid #CCCCCC;width:140px;'> <b>Requirement Type</b> </td> </tr>";
        //            for (int i = 0; i < Communicationdata.Tables[2].Rows.Count; i++)
        //            {
        //                vReasons = vReasons + " <tr> <td style='border: thin Solid #CCCCCC;'> " + (i + 1) + " </td> <td style='border: thin Solid #CCCCCC;'> " + Communicationdata.Tables[2].Rows[i]["IRReason"].ToString() + " </td> <td style='border: thin Solid #CCCCCC;'> " + Communicationdata.Tables[2].Rows[i]["Amount"].ToString() + " </td> <td style='border: thin Solid #CCCCCC;'> " + Communicationdata.Tables[2].Rows[i]["RequirementType"].ToString() + " </td> <td style='border: thin Solid #CCCCCC;'> " + Communicationdata.Tables[2].Rows[i]["isReceived"].ToString() + " </td> </tr>";
        //            }
        //            vReasons = vReasons + "</table><br>";
        //            EmailBody = EmailBody.Replace("&lt;&lt;" + "QueryRemarks" + "&gt;&gt;", vReasons);

        //            //_objCommon.CommonForSendingMail(EmailSubject, FromMailID, ToEmail, CCEmail, BCCEmail, EmailBody, false);

        //            int UserregionId = Convert.ToInt32(Session[SessionValue.UserRegionID]);
        //            _objClaimsVM.CommunicationTransactionInsert(ClaimID, SlNo, StageId, 86, FromMailID, ToEmail, CCEmail, BCCEmail, EmailSubject, EmailBody, Convert.ToInt32(Communicationdata.Tables[0].Rows[0]["AttributeID"].ToString()), DateTime.Now, UserregionId, false);
        //        }

        //    }
        //    catch (Exception ex)
        //    {
        //        new CommonController().ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "CommunicatingQuerypending-MedicalScrutiny", Session[Resources.SessionValue.LoginUserID].ToString());
        //        throw ex;
        //    }
        //}

        //public void CommunicatingRejectedReasons(ref DataSet Communicationdata, long? ClaimID, Byte SlNo, long MemberPolicyID, long? PolicyID, long ProviderID, int? BrokerID, long? CorpID, long? PayerID, int? IssueID)
        //{
        //    try
        //    {
        //        CommonController _objCommon = new CommonController();
        //        DataTable dt = null;
        //        dt = _objCommon.GetEmailCommunicationDetails(Communicationdata.Tables[0].Rows[0]["Entity_To"].ToString(), Communicationdata.Tables[0].Rows[0]["Entity_CC"].ToString(), Communicationdata.Tables[0].Rows[0]["Entity_BCC"].ToString(), IssueID, CorpID, PayerID, PolicyID, ProviderID, MemberPolicyID, ClaimID, BrokerID);

        //        DataRow row = dt.NewRow();
        //        row["Email"] = Convert.ToString(Communicationdata.Tables[0].Rows[0]["Email_To"]);
        //        row["Email_CC"] = Convert.ToString(Communicationdata.Tables[0].Rows[0]["Email_cc"]);
        //        row["Email_BCC"] = Convert.ToString(Communicationdata.Tables[0].Rows[0]["Email_BCC"]);
        //        dt.Rows.Add(row);

        //        string ToEmail, CCEmail, BCCEmail; ToEmail = CCEmail = BCCEmail = "";
        //        for (int i = 0; i < dt.Rows.Count; i++)
        //        {
        //            if (dt.Rows[i]["Email"].ToString() != "") ToEmail += "," + dt.Rows[i]["Email"];
        //            if (dt.Rows[i]["Email_CC"].ToString() != "") CCEmail += "," + dt.Rows[i]["Email_CC"];
        //            if (dt.Rows[i]["Email_BCC"].ToString() != "") BCCEmail += "," + dt.Rows[i]["Email_BCC"];
        //        }

        //        ////string FromMailID = ConfigurationManager.AppSettings["FromMail"].ToString();
        //        string FromMailID = Convert.ToString(Communicationdata.Tables[0].Rows[0]["email_from"]);
        //        string EmailBody = string.Empty;
        //        string EmailSubject = string.Empty;
        //        _objCommon.FormatHtmlTemplate(Communicationdata.Tables[1], Communicationdata.Tables[0].Rows[0]["Email_Body"].ToString(), out EmailBody);
        //        _objCommon.FormatHtmlTemplate(Communicationdata.Tables[1], Communicationdata.Tables[0].Rows[0]["Email_Subject"].ToString(), out EmailSubject);

        //        string vReasons = "";
        //        vReasons = vReasons + "<br><table style='border: thin Solid #CCCCCC; LINE-HEIGHT: 25px; FONT-SIZE:13px; FONT-FAMILY: verdana'> <tr> <td style='border: thin Solid #CCCCCC;width:60px;'><b> Sl No</b> </td> <td style='border: thin Solid #CCCCCC;'><b>Rejected Reasons </b> </td>  </tr>";
        //        for (int i = 0; i < Communicationdata.Tables[2].Rows.Count; i++)
        //        {
        //            vReasons = vReasons + " <tr> <td style='border: thin Solid #CCCCCC;'> " + (i + 1) + " </td> <td style='border: thin Solid #CCCCCC;'> " + Communicationdata.Tables[2].Rows[i]["Remarks"].ToString() + " </td>  </tr>";
        //        }
        //        vReasons = vReasons + "</table><br>";
        //        EmailBody = EmailBody.Replace("&lt;&lt;" + "Remarks" + "&gt;&gt;", vReasons);

        //        //_objCommon.CommonForSendingMail(EmailSubject, FromMailID, ToEmail, CCEmail, BCCEmail, EmailBody, false);

        //        int UserregionId = Convert.ToInt32(Session[SessionValue.UserRegionID]);
        //        _objClaimsVM.CommunicationTransactionInsert(ClaimID, SlNo, StageId, 86, FromMailID, ToEmail, CCEmail, BCCEmail, EmailSubject, EmailBody, Convert.ToInt32(Communicationdata.Tables[0].Rows[0]["AttributeID"].ToString()), DateTime.Now, UserregionId, false);
        //    }
        //    catch (Exception ex)
        //    {
        //        new CommonController().ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "CommunicatingRejectedReasons", Session[Resources.SessionValue.LoginUserID].ToString());
        //        throw ex;
        //    }
        //}

        /// <summary>
        /// From CRM Closing Remarks Insert
        /// </summary>
        /// <param name="ClaimDetails"></param>
        /// <returns></returns>
        public string ClaimFromCRMRemarks_Insert(string ClaimDetails, long ActionID, string QMSID, string QMSAdminID)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    string msg;
                    //  DataTable lst1 = (DataTable)JsonConvert.DeserializeObject(ClaimDetails, (typeof(DataTable)));
                    Newtonsoft.Json.Linq.JObject JObject = Newtonsoft.Json.Linq.JObject.Parse(ClaimDetails);
                    ClaimActionItems objActionIteams = new ClaimActionItems();
                    objActionIteams.ClaimID = Convert.ToInt64(JObject["ClaimID"]);
                    objActionIteams.Slno = Convert.ToInt16(JObject["Slno"]);
                    objActionIteams.ClaimTypeID = Convert.ToInt16(JObject["ClaimTypeID"]);
                    objActionIteams.RequestTypeID = Convert.ToInt16(JObject["RequestTypeID"]);
                    objActionIteams.ServiceTypeID = Convert.ToInt16(JObject["ServiceTypeID"]);
                    objActionIteams.ServiceSubTypeID = Convert.ToInt16(JObject["ServiceSubTypeID"]);
                    objActionIteams.ClaimStageID = Convert.ToInt32(JObject["ClaimStageID"]);
                    objActionIteams.RoleID = Convert.ToInt32(JObject["RoleID"]);
                    objActionIteams.RegionID = Convert.ToInt32(Session[Resources.SessionValue.RegionID]);
                    objActionIteams.ClaimedAmount = Convert.ToDecimal(JObject["ClaimedAmount"]);
                    objActionIteams.ReasonIDs_P = Convert.ToString(JObject["ReasonIDs_P"]);
                    objActionIteams.Remarks = Convert.ToString(JObject["Remarks"]);
                    objActionIteams.ClosedBy = Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]);
                    _objMadicalScrutinyVM.ClaimFromCRMRemarks_Insert(objActionIteams, ActionID, out msg);
                    //Claim Lock Release Code By Srinu B
                    new DefaultCacheProvider().Invalidate(Convert.ToString(JObject["ClaimID"]));

                    string QMS = string.Empty;
                    QMS = QMSID;
                    string QMSadmin = string.Empty;
                    QMSadmin = QMSAdminID;

                    Qmsv2CMController qms = new Qmsv2CMController();
                    qms.UpdateClaimStatus("UPDATESTATUS", "", "", "", "", QMS, "5", Session["UserRegionID"].ToString());

                    return Newtonsoft.Json.JsonConvert.SerializeObject(msg);
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "Adjudication_Actions_Insert", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        [HttpGet]
        [Authorize]
        public string Get_ClaimPreviousBankdetials(long ClaimID, int SlNo)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.Get_ClaimPreviousBankdetials(ClaimID, SlNo));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "ClaimCommunication_Retrieve", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }

        }

        [Authorize]
        public string SendNeftBouncedQueryLetter(long ClaimID, int SlNo, string EmailID, long Mobile, string Remarks, string PolicyType, string MainMemberPolicyID, string PolicyID, string ProviderID,
            string BrokerID, string PayerID, string CorporateID, string InsuranceCompanyID, int AgentID)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    string vMessage = string.Empty;
                    DataSet dsResult = null;
                    dsResult = _objMadicalScrutinyVM.SendNeftBouncedQueryLetter(ClaimID, SlNo, EmailID, Mobile, Remarks, Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]), out vMessage);

                    if (dsResult.Tables.Count != 0)
                    {
                        if (dsResult.Tables[0].Rows.Count > 0 && dsResult.Tables[1].Rows.Count > 0)//&& dsResult.Tables[2].Rows.Count > 0)
                        {

                            _objCommon.CommunicationInsert_Common(ref dsResult, ClaimID, SlNo, Convert.ToInt64(MainMemberPolicyID),
                                Convert.ToInt64(PolicyID), Convert.ToInt64(ProviderID), Convert.ToInt32(BrokerID), Convert.ToInt64(CorporateID),
                                Convert.ToInt64(PayerID), Convert.ToInt32(InsuranceCompanyID), 50, "MedicalScrutinyController", Convert.ToInt32(Session[SessionValue.UserRegionID]), 0, AgentID);
                        }
                    }

                    new DefaultCacheProvider().Invalidate(Convert.ToString(ClaimID));
                    // return vMessage;
                    ReturnError rtnObj = new ReturnError();
                    rtnObj.ID = 1;
                    rtnObj.Message = vMessage;
                    return Newtonsoft.Json.JsonConvert.SerializeObject(rtnObj);

                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                //return ex.Message;
                ReturnError rtnObj = new ReturnError();
                rtnObj.ID = 0;
                rtnObj.Message = ex.Message;
                return Newtonsoft.Json.JsonConvert.SerializeObject(rtnObj);

            }
        }

        [Authorize]
        public string NeftBouncedQueryResponseInsert(long ClaimID, int SlNo, string BankAccountNo, string BankName, string BranchName, string IFSCCode, int AccountTypeID, string EmailID, long Mobile, string Remarks)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    string vMessage = string.Empty;
                    _objMadicalScrutinyVM.NeftBouncedQueryResponseInsert(ClaimID, SlNo, BankAccountNo, BankName, BranchName, IFSCCode, AccountTypeID, EmailID, Mobile, Remarks, Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]), out vMessage);

                    new DefaultCacheProvider().Invalidate(Convert.ToString(ClaimID));
                    ReturnError rtnObj = new ReturnError();
                    rtnObj.ID = 1;
                    rtnObj.Message = vMessage;
                    return Newtonsoft.Json.JsonConvert.SerializeObject(rtnObj);
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                //return ex.Message;
                ReturnError rtnObj = new ReturnError();
                rtnObj.ID = 0;
                rtnObj.Message = ex.Message;
                return Newtonsoft.Json.JsonConvert.SerializeObject(rtnObj);

            }
        }

        [Authorize]
        public string DocumentComments_Retrieve(long ClaimID, int SlNo)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.DocumentComments_Retrieve(ClaimID, SlNo));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }


        [Authorize]
        public string GetPCSDetails(long ParentID)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.GetPCSDetails(ParentID));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        [Authorize]
        public string GetICDCodeDetails(long ParentID)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.GetICDCodeDetails(ParentID));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        [Authorize]
        public string BillingRemarks_Retrieve(long ClaimID, int SlNo)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.BillingRemarks_Retrieve(ClaimID, SlNo));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        #region Buffer
        [Authorize]
        public string SaveBufferDetails(string ClaimID, string SlNo, string MemberPolicyId, string ReferToId, string ReqAmount, string EligibleAmount, string RequestRemarks, int TPAProcID, Int64 RuleID, int SICategoryID_P20, bool? bufferwthoutbasecheck)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    int UserID = Convert.ToInt32(Session[SessionValue.LoginUserID]);
                    int DispatchedBranch = Convert.ToInt32(Session[SessionValue.UserRegionID]);

                    _objMadicalScrutinyVM.SaveBufferDetails(Convert.ToInt64(ClaimID), Convert.ToInt16(SlNo), Convert.ToInt64(MemberPolicyId)
                        , Convert.ToInt32(ReferToId), Convert.ToDouble(ReqAmount), Convert.ToDouble(EligibleAmount), RequestRemarks, TPAProcID, DispatchedBranch, UserID, RuleID, SICategoryID_P20, bufferwthoutbasecheck);

                    //SP3V-2500
                    string QMS = string.Empty;
                    if (TempData["QMS"] != null)
                    {
                        QMS = TempData["QMS"].ToString();
                    }
                    //SP3V-2500
                    //SP3V-2577
                    string QMSadmin = string.Empty;
                    if (TempData["QMSadmin"] != null)
                    {
                        QMSadmin = TempData["QMSadmin"].ToString();
                    }
                    //SP3V-2577
                    Qmsv2CMController qms = new Qmsv2CMController();
                    qms.UpdateClaimStatus("UPDATESTATUS", "", "", "", "", QMS, "5", Session["UserRegionID"].ToString());


                    return "Buffer Request Submitted Successfully";

                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "ClaimsController", "Save_BalanceSumInsured", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }
        [Authorize]
        public string UpdateBufferDetails(string ClaimID, string SlNo, string ResponseAmount, string ResponseRemarks, string DMSIds, int Status)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    //   int UserID = Convert.ToInt32(Session[SessionValue.LoginUserID]);
                    int UserLoginID = Convert.ToInt32(Session[SessionValue.UserRegionID]);

                    _objMadicalScrutinyVM.UpdateBufferDetails(Convert.ToInt64(ClaimID), Convert.ToInt16(SlNo), Convert.ToInt16(Status)
                       , Convert.ToDouble(ResponseAmount), ResponseRemarks, DMSIds, UserLoginID);

                    return "Buffer Approval Submitted Successfully";

                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "ClaimsController", "Save_BalanceSumInsured", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }
        [Authorize]
        public string CheckBufferClaim(long ClaimID, int SlNo)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.CheckBufferClaim(ClaimID, SlNo));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        [Authorize]
        public string IsBufferRulesConfigured(long BPSIId)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.IsBufferRulesConfigured(BPSIId));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        [Authorize]
        public string GetEligibleAmount_BKP(long ClaimID, long MemberPolicyID, long BPSIIdc, string ExcessSI)
        {
            double eligibleAmount = 0;
            string msg = string.Empty;
            double ESI = Convert.ToDouble(ExcessSI);
            StringBuilder strMsg = new StringBuilder();
            Int64 RuleID = 0;
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    DataSet ds = _objMadicalScrutinyVM.GetBufferDetails(ClaimID, MemberPolicyID, BPSIIdc);
                    if (ds.Tables.Count > 0 && ds.Tables.Count == 3)
                    {
                        //For Accedental case                       
                        DataRow drAccedent = ds.Tables[1].Select("isAccident=True").FirstOrDefault();
                        if (drAccedent != null && ds.Tables[0].Rows[0]["isAccident"].ToString() == "True")
                        {
                            if (drAccedent["isCovered"].ToString() == "False")
                            {
                                CheckNotCoveredConditions(ref strMsg, ds, drAccedent);
                                if (strMsg.ToString() == string.Empty)
                                {

                                }
                            }
                            else if (drAccedent["isCovered"].ToString() == "True")
                            {
                                GetAmount(ref eligibleAmount, ref strMsg, ESI, ds, drAccedent, ref RuleID);
                            }
                        }
                        else
                        {
                            //For Not covered case                       
                            DataRow drNotCovered = ds.Tables[1].Select("isCovered=False").FirstOrDefault();
                            if (drNotCovered != null)
                            {
                                // GetAmount(ref eligibleAmount, ref strMsg, ESI, ds, drNotCovered, ref RuleID);
                                CheckNotCoveredConditions(ref strMsg, ds, drNotCovered);
                                if (strMsg.ToString() == string.Empty)///IF Not covered conditions successfully excuted then we will excute covered rules
                                {
                                    foreach (DataRow dr in ds.Tables[1].Rows)
                                    {
                                        if (dr["isCovered"].ToString() == "True" && dr["isAccident"].ToString() == "False")
                                        {
                                            GetAmount(ref eligibleAmount, ref strMsg, ESI, ds, dr, ref RuleID);
                                        }

                                    }
                                }

                            }
                            else //if (strMsg.ToString() != string.Empty)
                            {
                                foreach (DataRow dr in ds.Tables[1].Rows)
                                {
                                    if (dr["isCovered"].ToString() == "True" && dr["isAccident"].ToString() == "False")
                                    {
                                        GetAmount(ref eligibleAmount, ref strMsg, ESI, ds, dr, ref RuleID);
                                    }

                                }
                            }
                        }

                    }
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { ErrorMsg = strMsg.ToString(), EligibleAmount = eligibleAmount, BufferRuleID = RuleID });

                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        [Authorize]
        public string GetEligibleAmount(long ClaimID, long MemberPolicyID, long BPSIIdc, string ExcessSI)
        {
            double eligibleAmount = 0;
            string msg = string.Empty;
            double ESI = Convert.ToDouble(ExcessSI);
            StringBuilder strMsg = new StringBuilder();
            bool isValid = true;
            Int64 RuleID = 0;
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    DataSet ds = _objMadicalScrutinyVM.GetBufferDetails(ClaimID, MemberPolicyID, BPSIIdc);
                    if (ds.Tables.Count > 0 && ds.Tables.Count == 3)
                    {

                        //For Not covered rules                       
                        bool notcoveredresult = true;
                        bool coveredresult = false;
                        foreach (DataRow dr in ds.Tables[1].Rows)
                        {
                            if (dr["isCovered"].ToString() == "False")
                            {
                                if (dr["isAccident"].ToString() == "True")
                                {
                                    if (ds.Tables[0].Rows[0]["isAccident_RTA"].ToString() == "True")
                                    {
                                        bool _isValid = CheckNotCoveredConditions(ref strMsg, ds, dr);
                                        notcoveredresult = _isValid;
                                        if (!notcoveredresult)
                                            strMsg.Append("Accident claim not matching with Benefitplan rules");
                                        strMsg.Append("Accident claims not covered as per Benefitplan rules");
                                        break;
                                    }
                                }
                                else
                                {
                                    bool _isValid = CheckNotCoveredConditions(ref strMsg, ds, dr);
                                    if (notcoveredresult) notcoveredresult = _isValid;
                                }
                            }

                        }
                        //For covered rules                       
                        if (notcoveredresult)
                        {
                            foreach (DataRow dr in ds.Tables[1].Rows)
                            {
                                if (dr["isCovered"].ToString() == "True")
                                {
                                    if (dr["isAccident"].ToString() == "True")
                                    {
                                        if (ds.Tables[0].Rows[0]["isAccident_RTA"].ToString() == "True")
                                        {
                                            bool _isValid = GetAmount(ref eligibleAmount, ref strMsg, ESI, ds, dr, ref RuleID);
                                            coveredresult = _isValid;
                                            if (!coveredresult)
                                                strMsg.Append("Accident claim not matching with Benefitplan rules");
                                            break;
                                        }
                                        else
                                        {
                                            bool anotheraccidentruleexists = false;
                                            foreach (DataRow drr in ds.Tables[1].Rows)
                                            {
                                                if (drr["isCovered"].ToString() == "True")
                                                {
                                                    if (drr["isAccident"].ToString() == "False")
                                                    {
                                                        anotheraccidentruleexists = true;
                                                    }
                                                }
                                            }
                                            if (!anotheraccidentruleexists)
                                            {
                                                strMsg.Append("Buffer Applicable for Accidents only");
                                                coveredresult = false;
                                            }
                                        }

                                    }
                                    else
                                    {
                                        bool _isValid = GetAmount(ref eligibleAmount, ref strMsg, ESI, ds, dr, ref RuleID);
                                        if (!coveredresult) coveredresult = _isValid;
                                    }
                                }
                            }
                        }

                        if (coveredresult && notcoveredresult)
                        {
                            isValid = true;
                        }
                        else
                        {
                            isValid = false;
                        }

                    }
                    if (!isValid)
                        eligibleAmount = 0;
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { ErrorMsg = strMsg.ToString(), EligibleAmount = eligibleAmount, BufferRuleID = RuleID });

                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        private void GetAmount_bkp(ref double eligibleAmount, ref StringBuilder msg, double ESI, DataSet ds, DataRow datarow, ref Int64 RuleID)
        {
            Int64.TryParse(datarow["RuleID"].ToString(), out RuleID);

            if (datarow["EffectiveDate"].ToString() != string.Empty)//effective date is not empty
            {
                if (Convert.ToDateTime(datarow["EffectiveDate"].ToString()) > Convert.ToDateTime(ds.Tables[0].Rows[0]["ClaimReceivedDate"].ToString()))
                {
                    msg.Append("EffectiveDate is greater than Claim received Date\n");
                    return;
                }

            }
            foreach (DataColumn dc in ds.Tables[1].Columns)
            {
                if (ds.Tables[0].Columns.Contains(dc.ColumnName))
                {
                    if (datarow[dc.ColumnName].ToString() != string.Empty)
                    {
                        if (dc.ColumnName == "RelGroupID_P26" || dc.ColumnName == "RelationshipID" || dc.ColumnName == "InsZone" || dc.ColumnName == "TPAProcedureID" || dc.ColumnName == "Grade"
                       || dc.ColumnName == "Designation" || dc.ColumnName == "RequestTypeID" || dc.ColumnName == "ServiceSubTypeID" || dc.ColumnName == "Accomdation")
                        {
                            string[] array = datarow[dc.ColumnName].ToString().Split(',');
                            if (!Array.Exists(array, element => element == ds.Tables[0].Rows[0][dc.ColumnName].ToString()))
                            {
                                //if (msg != string.Empty)
                                //    msg = msg + "," + dc.ColumnName;
                                //else
                                //    msg = dc.ColumnName;
                                msg.Append(dc.ColumnName + " is not matching with Rule configuration " + dc.ColumnName + "\t\n");
                            }
                        }

                        else if (Convert.ToString(datarow[dc.ColumnName]) != Convert.ToString(ds.Tables[0].Rows[0][dc.ColumnName]))
                        {
                            //if (msg != string.Empty)
                            //    msg = msg + "," + dc.ColumnName;
                            //else
                            //    msg = dc.ColumnName;
                            msg.Append(dc.ColumnName + " is not matching with Rule configuration " + dc.ColumnName + "\t\n");
                        }
                    }

                }
            }
            if (msg.ToString() != string.Empty)
            {
                return;
            }


            //Comment: Utilized Amounts
            double UtilizedCorpAmt = Convert.ToDouble(ds.Tables[2].Rows[0]["CorpLimit"].ToString());
            double UtilizedPolicyAmt = Convert.ToDouble(ds.Tables[2].Rows[0]["PolicyLimit"].ToString());
            double UtilizedFamilyAmt = Convert.ToDouble(ds.Tables[2].Rows[0]["FamilyLimit"].ToString());
            double UtilizedIndAmt = Convert.ToDouble(ds.Tables[2].Rows[0]["IndividualLimit"].ToString());
            double UtilizedClaimAmt = Convert.ToDouble(ds.Tables[2].Rows[0]["ClaimLimit"].ToString());

            //Rules Configured Amount
            double RuleCorpAmt = 0; double RulePolicyAmt = 0; double RuleFamilyAmt = 0; double RuleIndAmt = 0; double RuleClaimAmt = 0;
            double.TryParse(datarow["CorporateLimit"].ToString(), out RuleCorpAmt);
            double.TryParse(datarow["PolicyLimit"].ToString(), out RulePolicyAmt);
            double.TryParse(datarow["FamilyLimit"].ToString(), out RuleFamilyAmt);
            double.TryParse(datarow["IndividualLimit"].ToString(), out RuleIndAmt);
            double.TryParse(datarow["ClaimLimit"].ToString(), out RuleClaimAmt);
            ////Check Corporate Limit
            //if (RuleCorpAmt != 0)
            //{
            //    if (UtilizedCorpAmt >= RuleCorpAmt)
            //        goto PrintErrorMsg;

            //    else
            //    {
            //        eligibleAmount = (RuleCorpAmt > UtilizedCorpAmt) ? RuleCorpAmt - UtilizedCorpAmt : 0;

            //        if (eligibleAmount > ESI)
            //            eligibleAmount = ESI;
            //    }
            //}
            //if (RulePolicyAmt != 0)
            //{
            //    if (UtilizedPolicyAmt >= RulePolicyAmt)
            //        goto PrintErrorMsg;

            //    else
            //    {
            //        eligibleAmount = (RulePolicyAmt > UtilizedPolicyAmt) ? RulePolicyAmt - UtilizedPolicyAmt : 0;
            //        if (eligibleAmount > ESI)
            //            eligibleAmount = ESI;
            //    }

            //}
            //if (RuleFamilyAmt != 0)
            //{
            //    if (UtilizedFamilyAmt >= RuleFamilyAmt)
            //        goto PrintErrorMsg;

            //    else
            //    {
            //        eligibleAmount = (RuleFamilyAmt > UtilizedFamilyAmt) ? RuleFamilyAmt - UtilizedFamilyAmt : 0;
            //        if (eligibleAmount > ESI)
            //            eligibleAmount = ESI;
            //    }
            //}

            //if (RuleIndAmt != 0)
            //{
            //    if (UtilizedIndAmt >= RuleIndAmt)
            //        goto PrintErrorMsg;

            //    else
            //    {
            //        eligibleAmount = (RuleIndAmt > UtilizedIndAmt) ? RuleIndAmt - UtilizedIndAmt : 0;

            //        if (eligibleAmount > ESI)
            //            eligibleAmount = ESI;
            //    }
            //}
            //if (RuleClaimAmt != 0)
            //{
            //    if (UtilizedClaimAmt >= RuleClaimAmt)
            //        goto PrintErrorMsg;

            //    else
            //    {
            //        eligibleAmount = (RuleClaimAmt > UtilizedClaimAmt) ? RuleClaimAmt - UtilizedClaimAmt : 0;
            //        if (eligibleAmount > ESI)
            //            eligibleAmount = ESI;
            //    }
            //}
            double MinEligibleAmount = 0;
            //Check Corporate Limit
            if (RuleCorpAmt != 0)
            {
                if (UtilizedCorpAmt >= RuleCorpAmt)
                {
                    //goto PrintErrorMsg; 
                    RuleErrors("Buffer Corporate Limit exhausted", ref msg);
                }

                else
                {
                    eligibleAmount = (RuleCorpAmt > UtilizedCorpAmt) ? RuleCorpAmt - UtilizedCorpAmt : 0;

                    if (eligibleAmount > ESI)
                        eligibleAmount = ESI;
                    MinEligibleAmount = eligibleAmount;
                    if (eligibleAmount > MinEligibleAmount)
                        eligibleAmount = MinEligibleAmount;
                }
            }

            if (RulePolicyAmt != 0)
            {
                if (UtilizedPolicyAmt >= RulePolicyAmt)
                {
                    //goto PrintErrorMsg; 
                    RuleErrors("Buffer Corporate Limit exhausted", ref msg);
                }

                else
                {
                    eligibleAmount = (RulePolicyAmt > UtilizedPolicyAmt) ? RulePolicyAmt - UtilizedPolicyAmt : 0;
                    if (eligibleAmount > ESI)
                        eligibleAmount = ESI;
                    if (MinEligibleAmount == 0)
                        MinEligibleAmount = eligibleAmount;
                    else
                    {
                        if (eligibleAmount > MinEligibleAmount)
                            eligibleAmount = MinEligibleAmount;
                        else
                            MinEligibleAmount = eligibleAmount;
                    }

                }

            }
            if (RuleFamilyAmt != 0)
            {
                if (UtilizedFamilyAmt >= RuleFamilyAmt)
                {
                    //goto PrintErrorMsg; 
                    RuleErrors("Buffer Corporate Limit exhausted", ref msg);
                }

                else
                {
                    eligibleAmount = (RuleFamilyAmt > UtilizedFamilyAmt) ? RuleFamilyAmt - UtilizedFamilyAmt : 0;
                    if (eligibleAmount > ESI)
                        eligibleAmount = ESI;
                    if (MinEligibleAmount == 0)
                        MinEligibleAmount = eligibleAmount;
                    else
                    {
                        if (eligibleAmount > MinEligibleAmount)
                            eligibleAmount = MinEligibleAmount;
                        else
                            MinEligibleAmount = eligibleAmount;
                    }

                }
            }

            if (RuleIndAmt != 0)
            {
                if (UtilizedIndAmt >= RuleIndAmt)
                {
                    //goto PrintErrorMsg; 
                    RuleErrors("Buffer Corporate Limit exhausted", ref msg);
                }

                else
                {
                    eligibleAmount = (RuleIndAmt > UtilizedIndAmt) ? RuleIndAmt - UtilizedIndAmt : 0;

                    if (eligibleAmount > ESI)
                        eligibleAmount = ESI;
                    if (MinEligibleAmount == 0)
                        MinEligibleAmount = eligibleAmount;
                    else
                    {
                        if (eligibleAmount > MinEligibleAmount)
                            eligibleAmount = MinEligibleAmount;
                        else
                            MinEligibleAmount = eligibleAmount;
                    }

                }
            }
            if (RuleClaimAmt != 0)
            {
                if (UtilizedClaimAmt >= RuleClaimAmt)
                {
                    //goto PrintErrorMsg; 
                    RuleErrors("Buffer Corporate Limit exhausted", ref msg);
                }

                else
                {
                    eligibleAmount = (RuleClaimAmt > UtilizedClaimAmt) ? RuleClaimAmt - UtilizedClaimAmt : 0;
                    if (eligibleAmount > ESI)
                        eligibleAmount = ESI;
                    if (MinEligibleAmount == 0)
                        MinEligibleAmount = eligibleAmount;
                    else
                    {
                        if (eligibleAmount > MinEligibleAmount)
                            eligibleAmount = MinEligibleAmount;
                        else
                            MinEligibleAmount = eligibleAmount;
                    }

                }
            }

            return;

            //PrintErrorMsg:
            //    if (msg.ToString() != string.Empty)
            //    {
            //        //   msg = msg + "," + "Claim Limit Excceded";
            //        msg.Append("Claim Limit Excceded\t\n");
            //    }
            //    else
            //        msg.Append("Claim Limit Excceded\t\n");
        }

        private bool GetAmount(ref double eligibleAmount, ref StringBuilder msg, double ESI, DataSet ds, DataRow datarow, ref Int64 RuleID)
        {
            bool result = true;


            if (datarow["EffectiveDate"].ToString() != string.Empty)//effective date is not empty
            {
                if (Convert.ToDateTime(datarow["EffectiveDate"].ToString()) > Convert.ToDateTime(ds.Tables[0].Rows[0]["ClaimReceivedDate"].ToString()))
                {
                    msg.Append("EffectiveDate is greater than Claim received Date\n");
                    result = false;
                    return result;
                }

            }
            foreach (DataColumn dc in ds.Tables[1].Columns)
            {
                if (ds.Tables[0].Columns.Contains(dc.ColumnName))
                {
                    if (datarow[dc.ColumnName].ToString() != string.Empty)
                    {
                        if (dc.ColumnName == "RelGroupID_P26" || dc.ColumnName == "RelationshipID" || dc.ColumnName == "InsZone" || dc.ColumnName == "TPAProcedureID" || dc.ColumnName == "Grade"
                       || dc.ColumnName == "Designation" || dc.ColumnName == "RequestTypeID" || dc.ColumnName == "ServiceSubTypeID" || dc.ColumnName == "Accomdation")
                        {
                            string[] array = datarow[dc.ColumnName].ToString().Split(',');
                            if (!Array.Exists(array, element => element == ds.Tables[0].Rows[0][dc.ColumnName].ToString()))
                            {
                                msg.Append(dc.ColumnName + " is not matching with Rule configuration " + dc.ColumnName + "\t\n");
                                result = false;
                            }
                        }

                        else if (Convert.ToString(datarow[dc.ColumnName]) != Convert.ToString(ds.Tables[0].Rows[0][dc.ColumnName]))
                        {
                            msg.Append(dc.ColumnName + " is not matching with Rule configuration " + dc.ColumnName + "\t\n");
                            result = false;
                        }
                    }

                }
            }
            if (!result)
                if (msg.ToString() != string.Empty)
                {
                    return result;
                }
            if (result)
            {
                msg.Clear();
            }
            Int64.TryParse(datarow["RuleID"].ToString(), out RuleID);

            //Comment: Utilized Amounts
            double UtilizedGroupAmt = Convert.ToDouble(ds.Tables[2].Rows[0]["GroupLimit"].ToString());
            double UtilizedCorpAmt = Convert.ToDouble(ds.Tables[2].Rows[0]["CorpLimit"].ToString());
            double UtilizedPolicyAmt = Convert.ToDouble(ds.Tables[2].Rows[0]["PolicyLimit"].ToString());
            double UtilizedFamilyAmt = Convert.ToDouble(ds.Tables[2].Rows[0]["FamilyLimit"].ToString());
            double UtilizedIndAmt = Convert.ToDouble(ds.Tables[2].Rows[0]["IndividualLimit"].ToString());
            double UtilizedClaimAmt = Convert.ToDouble(ds.Tables[2].Rows[0]["ClaimLimit"].ToString());

            //Rules Configured Amount
            double RuleGroupAmt = 0; double RuleCorpAmt = 0; double RulePolicyAmt = 0; double RuleFamilyAmt = 0; double RuleIndAmt = 0; double RuleClaimAmt = 0;
            double.TryParse(datarow["GroupLimit"].ToString(), out RuleGroupAmt);
            double.TryParse(datarow["CorporateLimit"].ToString(), out RuleCorpAmt);
            double.TryParse(datarow["PolicyLimit"].ToString(), out RulePolicyAmt);
            double.TryParse(datarow["FamilyLimit"].ToString(), out RuleFamilyAmt);
            double.TryParse(datarow["IndividualLimit"].ToString(), out RuleIndAmt);
            double.TryParse(datarow["ClaimLimit"].ToString(), out RuleClaimAmt);
            double MinEligibleAmount = 0;


            //Check Group Limit
            if (RuleGroupAmt != 0)
            {
                if (UtilizedGroupAmt >= RuleGroupAmt)
                {
                    RuleErrors("Buffer Group Limit exhausted", ref msg);
                    result = false;
                }

                else
                {
                    eligibleAmount = (RuleGroupAmt > UtilizedGroupAmt) ? RuleGroupAmt - UtilizedGroupAmt : 0;

                    if (eligibleAmount > ESI)
                        eligibleAmount = ESI;
                    MinEligibleAmount = eligibleAmount;
                    if (eligibleAmount > MinEligibleAmount)
                        eligibleAmount = MinEligibleAmount;
                }
            }

            //Check Corporate Limit
            if (RuleCorpAmt != 0)
            {
                if (UtilizedCorpAmt >= RuleCorpAmt)
                {
                    RuleErrors("Buffer Corporate Limit exhausted", ref msg);
                    result = false;
                }

                else
                {
                    eligibleAmount = (RuleCorpAmt > UtilizedCorpAmt) ? RuleCorpAmt - UtilizedCorpAmt : 0;

                    if (eligibleAmount > ESI)
                        eligibleAmount = ESI;
                    if (MinEligibleAmount == 0)
                        MinEligibleAmount = eligibleAmount;
                    else
                    {
                        if (eligibleAmount > MinEligibleAmount)
                            eligibleAmount = MinEligibleAmount;
                        else
                            MinEligibleAmount = eligibleAmount;
                    }
                }
            }

            if (RulePolicyAmt != 0)
            {
                if (UtilizedPolicyAmt >= RulePolicyAmt)
                {
                    RuleErrors("Buffer Policy Limit exhausted", ref msg); result = false;
                }

                else
                {
                    eligibleAmount = (RulePolicyAmt > UtilizedPolicyAmt) ? RulePolicyAmt - UtilizedPolicyAmt : 0;
                    if (eligibleAmount > ESI)
                        eligibleAmount = ESI;
                    if (MinEligibleAmount == 0)
                        MinEligibleAmount = eligibleAmount;
                    else
                    {
                        if (eligibleAmount > MinEligibleAmount)
                            eligibleAmount = MinEligibleAmount;
                        else
                            MinEligibleAmount = eligibleAmount;
                    }

                }

            }
            if (RuleFamilyAmt != 0)
            {
                if (UtilizedFamilyAmt >= RuleFamilyAmt)
                {
                    RuleErrors("Buffer Family Limit exhausted", ref msg); result = false;
                }

                else
                {
                    eligibleAmount = (RuleFamilyAmt > UtilizedFamilyAmt) ? RuleFamilyAmt - UtilizedFamilyAmt : 0;
                    if (eligibleAmount > ESI)
                        eligibleAmount = ESI;
                    if (MinEligibleAmount == 0)
                        MinEligibleAmount = eligibleAmount;
                    else
                    {
                        if (eligibleAmount > MinEligibleAmount)
                            eligibleAmount = MinEligibleAmount;
                        else
                            MinEligibleAmount = eligibleAmount;
                    }

                }
            }

            if (RuleIndAmt != 0)
            {
                if (UtilizedIndAmt >= RuleIndAmt)
                {
                    RuleErrors("Buffer Individual Limit exhausted", ref msg); result = false;
                }

                else
                {
                    eligibleAmount = (RuleIndAmt > UtilizedIndAmt) ? RuleIndAmt - UtilizedIndAmt : 0;

                    if (eligibleAmount > ESI)
                        eligibleAmount = ESI;
                    if (MinEligibleAmount == 0)
                        MinEligibleAmount = eligibleAmount;
                    else
                    {
                        if (eligibleAmount > MinEligibleAmount)
                            eligibleAmount = MinEligibleAmount;
                        else
                            MinEligibleAmount = eligibleAmount;
                    }

                }
            }
            if (RuleClaimAmt != 0)
            {
                if (UtilizedClaimAmt >= RuleClaimAmt)
                {
                    RuleErrors("Buffer Claim Limit exhausted", ref msg); result = false;
                }

                else
                {
                    eligibleAmount = (RuleClaimAmt > UtilizedClaimAmt) ? RuleClaimAmt - UtilizedClaimAmt : 0;
                    if (eligibleAmount > ESI)
                        eligibleAmount = ESI;
                    if (MinEligibleAmount == 0)
                        MinEligibleAmount = eligibleAmount;
                    else
                    {
                        if (eligibleAmount > MinEligibleAmount)
                            eligibleAmount = MinEligibleAmount;
                        else
                            MinEligibleAmount = eligibleAmount;
                    }

                }
            }

            return result;

        }

        public void RuleErrors(string Msg, ref StringBuilder str)
        {
            try
            {
                str.Append(Msg);
                str.Append("\t\n");
            }
            catch (Exception ex)
            {
                str.Append("Error occured while checking rules.\t" + ex.Message + "\t\n");
            }
        }

        private bool CheckNotCoveredConditions(ref StringBuilder msg, DataSet ds, DataRow datarow)
        {
            bool result = true;
            foreach (DataColumn dc in ds.Tables[1].Columns)///BP Rules
            {
                if (ds.Tables[0].Columns.Contains(dc.ColumnName))//Claim values
                {
                    if (datarow[dc.ColumnName].ToString() != string.Empty)
                    {
                        if (dc.ColumnName == "RelGroupID_P26" || dc.ColumnName == "RelationshipID" || dc.ColumnName == "InsZone" || dc.ColumnName == "TPAProcedureID" || dc.ColumnName == "Grade"
                       || dc.ColumnName == "Designation" || dc.ColumnName == "RequestTypeID" || dc.ColumnName == "ServiceSubTypeID" || dc.ColumnName == "Accomdation")
                        {
                            string[] array = datarow[dc.ColumnName].ToString().Split(',');
                            if (Array.Exists(array, element => element == ds.Tables[0].Rows[0][dc.ColumnName].ToString()))
                            {
                                //if (msg != string.Empty)
                                //    msg = msg + "," + dc.ColumnName;
                                //else
                                //    msg = dc.ColumnName;
                                msg.Append(dc.ColumnName + " is not matching with Rule configuration " + dc.ColumnName + "\t\n");
                                result = false;
                            }
                        }

                        else if (Convert.ToString(datarow[dc.ColumnName]) == Convert.ToString(ds.Tables[0].Rows[0][dc.ColumnName]))
                        {
                            //if (msg != string.Empty)
                            //    msg = msg + "," + dc.ColumnName;
                            //else
                            //    msg = dc.ColumnName;
                            msg.Append(dc.ColumnName + " is not matching with Rule configuration " + dc.ColumnName + "\t\n");
                            result = false;
                        }
                    }

                }
            }
            return result;
            //if (msg.ToString() != string.Empty)
            //{
            //    return;
            //}
        }


        #endregion

        public string GetBSI(long MemberPolicyID, int SITypeID, long ClaimID, byte SlNo)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    BSIinfo objBSI = new Main().GetBSI(MemberPolicyID, SITypeID, ClaimID, SlNo);
                    return Newtonsoft.Json.JsonConvert.SerializeObject(objBSI);
                }
                else
                {
                    return "ErrorCode#1";
                }

            }
            catch (Exception ex)
            {

                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }


        public string Clone_Billnigandcoding(Int64 ClaimID, int SlNo, Int16 RequestTypeID)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    int UserRegionId = Convert.ToInt16(Session[SessionValue.UserRegionID]);

                    string vMessage = string.Empty;
                    string vMessagee = string.Empty;

                    Newtonsoft.Json.JsonConvert.SerializeObject(_objClaimsVM.Clone_Billnigandcoding(ClaimID, SlNo, UserRegionId, out vMessage));
                    DataTable IsBuffer = _objClaimsVM.IsBufferUtilized(ClaimID, Convert.ToInt16(SlNo)); //added by Bhagyaraj for SP-1216 
                    string IsBuffereenable = IsBuffer.Rows[0]["Buffercount"].ToString();
                    if (Convert.ToInt16(IsBuffereenable) == 1 && RequestTypeID == 4)
                    {
                        int di = _objMadicalScrutinyVM.Save_BufferDetails(ClaimID, Convert.ToInt16(SlNo), Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]), out vMessagee);
                    }
                    return Newtonsoft.Json.JsonConvert.SerializeObject(vMessage);
                    //return vMessage;
                }
                else
                {
                    return "ErrorCode#1";
                }

            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }





        }

        [HttpGet]
        [Authorize]
        public string GetCoverageEligibility(long ClaimID, long BPSIID, int CoverageID)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    DataSet ds = _objMadicalScrutinyVM.GetClaimBPCoverageDetials(ClaimID, BPSIID, CoverageID);
                    return CheckCoverageEligibility(ds);
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "ClaimCommunication_Retrieve", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }

        }




        public class CoverageEligiblity
        {
            public long RuleID { get; set; }
            public Double Claimed { get; set; }
            public Double Eligible { get; set; }
            public Double Deduction { get; set; }
            public Double Sanctioned { get; set; }
            public bool IsEligible { get; set; }
            public string Message { get; set; }
            public bool IsOutofSI { get; set; }
            public string RuleDescription { get; set; }
        }

        public string CheckCoverageEligibility(DataSet ds)
        {
            double eligibleAmount = 0;
            double ClaimAmt = 0;
            string msg = string.Empty;
            StringBuilder strMsg = new StringBuilder();
            bool _isValid = true;
            Int64 RuleID = 0;

            CoverageEligiblity _eligibility = new CoverageEligiblity();
            try
            {

                if (ds.Tables.Count > 0)
                {
                    foreach (DataRow dr in ds.Tables[1].Rows)
                    {
                        if (dr["isCovered"].ToString() == "True")
                        {
                            RuleID = Convert.ToInt64(dr["RuleID"]);

                            //_eligibility.Eligible = Convert.ToDouble(dr["ExternalValueAbs"]);
                            _isValid = GetCoverageEligilbleAmount(ref eligibleAmount, ref ClaimAmt, ref strMsg, ds, dr, ref RuleID);
                            _eligibility.Claimed = ClaimAmt;
                            _eligibility.IsEligible = _isValid;
                            _eligibility.RuleID = RuleID;
                            if (Convert.ToString(dr["CoverageType_P49"]) != "")
                            {
                                if (Convert.ToString(dr["CoverageType_P49"]) == "193")
                                    _eligibility.IsOutofSI = true;
                                else
                                    _eligibility.IsOutofSI = false;
                            }
                            if (Convert.ToString(dr["Remarks"]) != "")
                            {
                                _eligibility.RuleDescription = Convert.ToString(dr["Remarks"]);
                            }
                            if (_isValid)
                            {
                                _eligibility.Eligible = eligibleAmount;
                                _eligibility.Deduction = ClaimAmt - eligibleAmount;
                            }
                            else
                            {
                                _eligibility.Eligible = 0;
                                _eligibility.Deduction = ClaimAmt;
                            }
                            _eligibility.Message = strMsg.ToString();
                        }

                    }
                    #region Not in use
                    ////For Not covered case                       
                    //DataRow drNotCovered = ds.Tables[1].Select("isCovered=False").FirstOrDefault();
                    //if (drNotCovered != null)
                    //{
                    //    //CheckNotCoveredConditions(ref strMsg, ds, drNotCovered);
                    //    //if (strMsg.ToString() == string.Empty)///IF Not covered conditions successfully excuted then we will excute covered rules
                    //    //{
                    //    //    foreach (DataRow dr in ds.Tables[1].Rows)
                    //    //    {
                    //    //        if (dr["isCovered"].ToString() == "True")
                    //    //        {
                    //    //            GetAmount(ref eligibleAmount, ref strMsg, ESI, ds, dr, ref RuleID);
                    //    //        }

                    //    //    }
                    //    //}
                    //          //    foreach (DataRow dr in ds.Tables[1].Rows)
                    //    //    {
                    //    //        if (dr["isCovered"].ToString() == "True")
                    //    //        {
                    //    //            GetAmount(ref eligibleAmount, ref strMsg, ESI, ds, dr, ref RuleID);
                    //    //        }

                    //    //    }
                    //    _eligibility.IsEligible = false;

                    //}
                    //else
                    //{
                    //    _eligibility.IsEligible = true;
                    //    //foreach (DataRow dr in ds.Tables[1].Rows)
                    //    //{
                    //    //    if (dr["isCovered"].ToString() == "True")
                    //    //    {
                    //    //        GetAmount(ref eligibleAmount, ref strMsg, ESI, ds, dr, ref RuleID);
                    //    //    }

                    //    //}
                    //}
                    #endregion
                }
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
            }

            return Newtonsoft.Json.JsonConvert.SerializeObject(_eligibility);
        }

        [HttpGet]
        [Authorize]
        public string ProviderDetails_Retrieve(long ClaimID, long ProviderID, long MemberPolicyID, DateTime? DOA, bool IsFrmArchived = false)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.Provider_Retrive(ClaimID, ProviderID, MemberPolicyID, DOA, IsFrmArchived));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "ClaimCommunication_Retrieve", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }

        }

        private bool GetCoverageEligilbleAmount(ref double eligibleAmount, ref double ClaimAmt, ref StringBuilder msg, DataSet ds, DataRow datarow, ref Int64 RuleID)
        {
            bool result = true;

            bool isDaywiseConfig = false;
            if (datarow["EffectiveDate"].ToString() != string.Empty)//effective date is not empty
            {
                if (Convert.ToDateTime(datarow["EffectiveDate"].ToString()) > Convert.ToDateTime(ds.Tables[0].Rows[0]["ClaimReceivedDate"].ToString()))
                {
                    msg.Append("EffectiveDate is greater than Claim received Date\n");
                    result = false;
                    return result;
                }

            }
            foreach (DataColumn dc in ds.Tables[1].Columns)
            {
                if (ds.Tables[0].Columns.Contains(dc.ColumnName))
                {
                    if (datarow[dc.ColumnName].ToString() != string.Empty)
                    {
                        if (dc.ColumnName == "RelGroupID_P26" || dc.ColumnName == "RelationshipID" || dc.ColumnName == "InsZone" || dc.ColumnName == "TPAProcedureID" || dc.ColumnName == "Grade"
                       || dc.ColumnName == "Designation" || dc.ColumnName == "RequestTypeID" || dc.ColumnName == "ServiceSubTypeID" || dc.ColumnName == "Accomdation")
                        {
                            string[] array = datarow[dc.ColumnName].ToString().Split(',');
                            if (!Array.Exists(array, element => element == ds.Tables[0].Rows[0][dc.ColumnName].ToString()))
                            {
                                msg.Append(dc.ColumnName + " is not matching with Rule configuration " + dc.ColumnName + "\t\n");
                                result = false;
                            }
                        }
                        else if (dc.ColumnName == "Age")
                        {
                            int AgeExpression = 0;
                            if (Convert.ToString(datarow["LimitCatg_P29"]) != string.Empty)
                                AgeExpression = Convert.ToInt32(datarow["LimitCatg_P29"]);
                            int AgeType = 0;
                            if (Convert.ToString(datarow["AgeTypeID"]) != string.Empty)
                                AgeType = Convert.ToInt32(datarow["AgeTypeID"]);

                            if (Convert.ToString(datarow["AgeTypeID"]) != Convert.ToString(ds.Tables[0].Rows[0]["AgeTypeID"]))
                            {
                                msg.Append("Age Type is not matching with Rule configuration Age Type \t\n");
                                result = false;
                            }

                            if (AgeExpression == 53)  //==
                            {
                                if (Convert.ToInt32(datarow[dc.ColumnName]) != Convert.ToInt32(ds.Tables[0].Rows[0][dc.ColumnName]))
                                {
                                    msg.Append(dc.ColumnName + " is not matching with Rule configuration " + dc.ColumnName + "\t\n");
                                    result = false;
                                }
                            }
                            else if (AgeExpression == 54)//>
                            {
                                if (Convert.ToInt32(datarow[dc.ColumnName]) > Convert.ToInt32(ds.Tables[0].Rows[0][dc.ColumnName]))
                                {
                                    msg.Append(dc.ColumnName + " is not matching with Rule configuration " + dc.ColumnName + "\t\n");
                                    result = false;
                                }
                            }
                            else if (AgeExpression == 55)//<
                            {
                                if (Convert.ToInt32(datarow[dc.ColumnName]) < Convert.ToInt32(ds.Tables[0].Rows[0][dc.ColumnName]))
                                {
                                    msg.Append(dc.ColumnName + " is not matching with Rule configuration " + dc.ColumnName + "\t\n");
                                    result = false;
                                }
                            }
                            else if (AgeExpression == 56)//>=
                            {
                                if (Convert.ToInt32(datarow[dc.ColumnName]) >= Convert.ToInt32(ds.Tables[0].Rows[0][dc.ColumnName]))
                                {
                                    msg.Append(dc.ColumnName + " is not matching with Rule configuration " + dc.ColumnName + "\t\n");
                                    result = false;
                                }
                            }
                            else if (AgeExpression == 57)//<=
                            {
                                if (Convert.ToInt32(datarow[dc.ColumnName]) <= Convert.ToInt32(ds.Tables[0].Rows[0][dc.ColumnName]))
                                {
                                    msg.Append(dc.ColumnName + " is not matching with Rule configuration " + dc.ColumnName + "\t\n");
                                    result = false;
                                }
                            }

                        }

                        else if (Convert.ToString(datarow[dc.ColumnName]) != Convert.ToString(ds.Tables[0].Rows[0][dc.ColumnName]))
                        {
                            msg.Append(dc.ColumnName + " is not matching with Rule configuration " + dc.ColumnName + "\t\n");
                            result = false;
                        }
                    }

                }

                if (dc.ColumnName == "SpecialRuleCondition")
                {
                    int SpecialCondition = 0;
                    SpecialCondition = Convert.ToInt32(datarow["SpecialRuleCondition"]);
                    if (SpecialCondition == 399)//When Critical Illness Claims
                    {
                        if (Convert.ToBoolean(ds.Tables[0].Rows[0]["isCI"]) == false)
                        {
                            msg.Append("This Coverage is applicable for Critical illness claims only. This is Violating with Rule configuration " + dc.ColumnName + "\t\n");
                            result = false;
                        }
                    }
                    else if (SpecialCondition == 2)//when Claim free
                    {
                        if (Convert.ToBoolean(ds.Tables[0].Rows[0]["isClaimFree"]) == false)
                        {
                            msg.Append("This Coverage is applicable for Claim free policies only. This is Violating with Rule configuration " + dc.ColumnName + "\t\n");
                            result = false;
                        }
                    }
                    else if (SpecialCondition == 400)//when PPN Hospital
                    {
                        if (Convert.ToBoolean(ds.Tables[0].Rows[0]["IsPPN"]) == false)
                        {
                            msg.Append("This Coverage is applicable for PPN Network Hospital only. This is Violating with Rule configuration " + dc.ColumnName + "\t\n");
                            result = false;
                        }
                    }
                }
                if ((dc.ColumnName == "ExternalValueAbs") && (Convert.ToString(datarow["ExternalValueAbs"]) != string.Empty))//Per day limit rules
                {
                    isDaywiseConfig = true;
                    int Duration = 0;
                    if (Convert.ToString(datarow["Duration"]) != string.Empty)
                        Duration = Convert.ToInt32(datarow["Duration"]);
                    int DeductibleDays = 0;
                    if (Convert.ToString(datarow["IndividualClaimCount"]) != string.Empty)
                        DeductibleDays = Convert.ToInt32(datarow["IndividualClaimCount"]);
                    int DurationType = 0;
                    if (Convert.ToString(datarow["DurationType_P18"]) != string.Empty)
                        DurationType = Convert.ToInt32(datarow["DurationType_P18"]);
                    int LOS = 0;
                    if (Convert.ToString(ds.Tables[0].Rows[0]["LOS"]) != string.Empty)
                        LOS = Convert.ToInt32(ds.Tables[0].Rows[0]["LOS"]);


                    if (DurationType != 61)
                    {
                        msg.Append("Duration Type should be Days in Rule configuration.\t\n");
                        result = false;
                    }
                    if (result)
                        if (LOS < Duration * 24)
                        {
                            msg.Append("LOS should be more than or equal to " + Duration + " days. This is Violating with Rule configuration " + dc.ColumnName + "\t\n");
                            result = false;
                        }

                    if (result)
                    {
                        if (Math.Round(Convert.ToDouble((LOS / 24) - DeductibleDays)) > 0)
                            ClaimAmt = ((LOS / 24) - DeductibleDays) * Convert.ToDouble(datarow[dc.ColumnName]);
                    }
                }
                else if ((dc.ColumnName == "BPComparisionFrom_P52") && (Convert.ToString(datarow["BPComparisionFrom_P52"]) != string.Empty))//Comparision Conditions
                {

                    int CFrom = 0;
                    if (Convert.ToString(datarow["BPComparisionFrom_P52"]) != string.Empty)
                        CFrom = Convert.ToInt32(datarow["BPComparisionFrom_P52"]);
                    int Expression = 0;
                    if (Convert.ToString(datarow["ExpressionID_P17"]) != string.Empty)
                        Expression = Convert.ToInt32(datarow["ExpressionID_P17"]);

                    int Duration = 0;
                    if (Convert.ToString(datarow["Duration"]) != string.Empty)
                        Duration = Convert.ToInt32(datarow["Duration"]);
                    //int DeductibleDays = 0;
                    //if (Convert.ToString(datarow["IndividualClaimCount"]) != string.Empty)
                    //    DeductibleDays = Convert.ToInt32(datarow["IndividualClaimCount"]);
                    int DurationType = 0;
                    if (Convert.ToString(datarow["DurationType_P18"]) != string.Empty)
                        DurationType = Convert.ToInt32(datarow["DurationType_P18"]);

                    int CTo = 0;
                    if (Convert.ToString(datarow["BPComparisionTo_P52"]) != string.Empty)
                        CTo = Convert.ToInt32(datarow["BPComparisionTo_P52"]);



                    if (CFrom == 208)//208--claim amount 
                    {
                        Double ClaimAmount = 0;
                        if (Convert.ToString(ds.Tables[0].Rows[0]["ClaimAmount"]) != string.Empty)
                            ClaimAmount = Convert.ToDouble(ds.Tables[0].Rows[0]["ClaimAmount"]);

                        if (DurationType != 410)//410--Rupees
                        {
                            msg.Append("Duration/Formula Type should be Rupees while Comparision with Claim Amount in Rule configuration.\t\n");
                            result = false;
                        }
                        if (CTo != 0)//410--Rupees
                        {
                            msg.Append("Comparision TO should not be configured while Comparision with Claim Amount in Rule configuration.\t\n");
                            result = false;
                        }
                        result = ComparisionExpression(Expression, Duration.ToString(), ClaimAmount.ToString(), "ClaimAmount", ref msg);
                        //result = ComparisionExpression(Expression,Duration.ToString(),ClaimAmount.ToString(),dc.ColumnName,ref msg);

                    }

                }
            }
            if (!result)
                if (msg.ToString() != string.Empty)
                {
                    return result;
                }
            if (result)
            {
                msg.Clear();
            }
            Int64.TryParse(datarow["RuleID"].ToString(), out RuleID);

            //Comment: Utilized Amounts
            double UtilizedCorpAmt = Convert.ToDouble(ds.Tables[2].Rows[0]["CorpLimit"].ToString());
            double UtilizedPolicyAmt = Convert.ToDouble(ds.Tables[2].Rows[0]["PolicyLimit"].ToString());
            double UtilizedFamilyAmt = Convert.ToDouble(ds.Tables[2].Rows[0]["FamilyLimit"].ToString());
            double UtilizedIndAmt = Convert.ToDouble(ds.Tables[2].Rows[0]["IndividualLimit"].ToString());
            double UtilizedClaimAmt = Convert.ToDouble(ds.Tables[2].Rows[0]["ClaimLimit"].ToString());

            //Rules Configured Amount
            double RuleCorpAmt = 0; double RulePolicyAmt = 0; double RuleFamilyAmt = 0; double RuleIndAmt = 0; double RuleClaimAmt = 0;

            double.TryParse(datarow["CorporateLimit"].ToString(), out RuleCorpAmt);
            double.TryParse(datarow["PolicyLimit"].ToString(), out RulePolicyAmt);
            double.TryParse(datarow["FamilyLimit"].ToString(), out RuleFamilyAmt);
            double.TryParse(datarow["IndividualLimit"].ToString(), out RuleIndAmt);
            double.TryParse(datarow["ClaimLimit"].ToString(), out RuleClaimAmt);

            double MinEligibleAmount = 0;

            //Check Corporate Limit
            if (RuleCorpAmt != 0)
            {
                if (UtilizedCorpAmt >= RuleCorpAmt)
                {
                    RuleErrors("Rule Corporate Limit exhausted", ref msg);
                    result = false;
                }

                else
                {
                    eligibleAmount = (RuleCorpAmt > UtilizedCorpAmt) ? RuleCorpAmt - UtilizedCorpAmt : 0;

                    MinEligibleAmount = eligibleAmount;
                    if (eligibleAmount > MinEligibleAmount)
                        eligibleAmount = MinEligibleAmount;
                }
            }

            if (RulePolicyAmt != 0)
            {
                if (UtilizedPolicyAmt >= RulePolicyAmt)
                {
                    RuleErrors("Rule Policy Limit exhausted", ref msg); result = false;
                }

                else
                {
                    eligibleAmount = (RulePolicyAmt > UtilizedPolicyAmt) ? RulePolicyAmt - UtilizedPolicyAmt : 0;

                    if (MinEligibleAmount == 0)
                        MinEligibleAmount = eligibleAmount;
                    else
                    {
                        if (eligibleAmount > MinEligibleAmount)
                            eligibleAmount = MinEligibleAmount;
                        else
                            MinEligibleAmount = eligibleAmount;
                    }

                }

            }
            if (RuleFamilyAmt != 0)
            {
                if (UtilizedFamilyAmt >= RuleFamilyAmt)
                {
                    RuleErrors("Rule Family Limit exhausted", ref msg); result = false;
                }

                else
                {
                    eligibleAmount = (RuleFamilyAmt > UtilizedFamilyAmt) ? RuleFamilyAmt - UtilizedFamilyAmt : 0;

                    if (MinEligibleAmount == 0)
                        MinEligibleAmount = eligibleAmount;
                    else
                    {
                        if (eligibleAmount > MinEligibleAmount)
                            eligibleAmount = MinEligibleAmount;
                        else
                            MinEligibleAmount = eligibleAmount;
                    }

                }
            }

            if (RuleIndAmt != 0)
            {
                if (UtilizedIndAmt >= RuleIndAmt)
                {
                    RuleErrors("Rule Individual Limit exhausted", ref msg); result = false;
                }

                else
                {
                    eligibleAmount = (RuleIndAmt > UtilizedIndAmt) ? RuleIndAmt - UtilizedIndAmt : 0;

                    if (MinEligibleAmount == 0)
                        MinEligibleAmount = eligibleAmount;
                    else
                    {
                        if (eligibleAmount > MinEligibleAmount)
                            eligibleAmount = MinEligibleAmount;
                        else
                            MinEligibleAmount = eligibleAmount;
                    }

                }
            }
            if (RuleClaimAmt != 0)
            {
                if (UtilizedClaimAmt >= RuleClaimAmt)
                {
                    RuleErrors("Rule Claim Limit exhausted", ref msg); result = false;
                }

                else
                {
                    eligibleAmount = (RuleClaimAmt > UtilizedClaimAmt) ? RuleClaimAmt - UtilizedClaimAmt : 0;

                    if (MinEligibleAmount == 0)
                        MinEligibleAmount = eligibleAmount;
                    else
                    {
                        if (eligibleAmount > MinEligibleAmount)
                            eligibleAmount = MinEligibleAmount;
                        else
                            MinEligibleAmount = eligibleAmount;
                    }

                }


            }
            if ((Convert.ToDouble(ds.Tables[0].Rows[0]["ClaimAmount"].ToString()) < eligibleAmount))  //SP-887
                eligibleAmount = Convert.ToDouble(ds.Tables[0].Rows[0]["ClaimAmount"].ToString());

            if (isDaywiseConfig)
            {
                if (ClaimAmt < eligibleAmount)
                {
                    eligibleAmount = ClaimAmt;
                }
                else if (RuleCorpAmt == 0 && RulePolicyAmt == 0 && RuleFamilyAmt == 0 && RuleIndAmt == 0 && RuleClaimAmt == 0)
                    eligibleAmount = ClaimAmt;
            }
            else
                ClaimAmt = eligibleAmount;
            return result;

        }

        public bool ComparisionExpression(int CompareExpression, string RuleValue, string ClaimValue, string ColumnName, ref StringBuilder msg)
        {

            bool result = true;
            if (CompareExpression == 53)  //==
            {
                if (Convert.ToInt32(ClaimValue) != Convert.ToInt32(RuleValue))
                {
                    msg.Append(ColumnName + " is not matching with Rule configuration " + ColumnName + "\t\n");
                    result = false;
                }
            }
            else if (CompareExpression == 54)//>
            {
                if (Convert.ToInt32(ClaimValue) <= Convert.ToInt32(RuleValue))
                {
                    msg.Append(ColumnName + " is not matching with Rule configuration " + ColumnName + "\t\n");
                    result = false;
                }
            }
            else if (CompareExpression == 55)//<
            {
                if (Convert.ToInt32(ClaimValue) >= Convert.ToInt32(RuleValue))
                {
                    msg.Append(ColumnName + " is not matching with Rule configuration " + ColumnName + "\t\n");
                    result = false;
                }
            }
            else if (CompareExpression == 56)//>=
            {
                if (Convert.ToInt32(ClaimValue) < Convert.ToInt32(RuleValue))
                {
                    msg.Append(ColumnName + " is not matching with Rule configuration " + ColumnName + "\t\n");
                    result = false;
                }
            }
            else if (CompareExpression == 57)//<=
            {
                if (Convert.ToInt32(ClaimValue) > Convert.ToInt32(RuleValue))
                {
                    msg.Append(ColumnName + " is not matching with Rule configuration " + ColumnName + "\t\n");
                    result = false;
                }
            }

            return result;
        }

        private string Save_ePreauthDetails(int StageID, long ClaimID, int SlNO)
        {
            try
            {
                string Msg = string.Empty;
                int vretunValue = 0;
                bool isEpreauthClaim = true;
                isEpreauthClaim = _objMadicalScrutinyVM.IsEpreauthClaim(ClaimID);
                if (isEpreauthClaim)
                {
                    string Location = ConfigurationManager.AppSettings["EpreauthLettersPath"].ToString();
                    int statusID = 0;
                    string status = string.Empty;

                    if (StageID == 24)
                    {
                        statusID = 2;
                        status = "Authorized";
                    }
                    if (StageID == 7)
                    {
                        statusID = 1;
                        status = "Pending";
                    }
                    if (StageID == 23)
                    {
                        statusID = 3;
                        status = "Rejected";
                    }
                    if (StageID == 21)
                    {
                        statusID = 6;
                    }
                    DataSet ds = _objMadicalScrutinyVM.GetDetailsUpdateEpreAuthStatus(ClaimID, SlNO, StageID);
                    //Epreauth Letter Generation
                    string LetterPath = Location;// Server.MapPath("~\\PDFFiles\\");
                    string TimeStamp = DateTime.Now.ToString("mmss");
                    string FolderPath = string.Format("{0:MM-yyyy}", DateTime.Now);
                    string SavePath = FolderPath;
                    string _SavePath = FolderPath;
                    FolderPath = LetterPath + FolderPath;
                    string EmailFileName = string.Empty;

                    try
                    {
                        if (!Directory.Exists(FolderPath))
                            Directory.CreateDirectory(FolderPath);
                        EmailFileName = ClaimID + "-" + TimeStamp + ".pdf";
                        SavePath = FolderPath + "\\" + EmailFileName;
                        _SavePath = _SavePath + "\\" + EmailFileName;
                        // SaveEpreauthLetter(ClaimID, Convert.ToString(ds.Tables[0].Rows[0]["LetterContent"]),SavePath);
                        if (ds.Tables[1].Rows.Count > 0)
                            HTMLToPdf(Convert.ToString(ds.Tables[1].Rows[0]["LetterContent"]), SavePath);
                    }
                    catch (Exception exe)
                    {
                        Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                        errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                        errorLog.Log(new Elmah.Error(exe));
                        Msg = "Letter generation got failed";
                    }
                    string vMessage = string.Empty;
                    //Updating Preauth Status in Epreauth Module after generating Letter along with Letter Path
                    WebshareUpdateLetterDetails objDetails = new WebshareUpdateLetterDetails();
                    objDetails.PreauthID = ClaimID;
                    objDetails.DataBaseName = "McarePlus";
                    objDetails.SlNo = SlNO;
                    objDetails.AuthorizingDoctorName = Session[SessionValue.LoginUserName].ToString();
                    objDetails.LastStatusUpdateDateTime = DateTime.Now;
                    objDetails.StatusID = statusID;
                    if (ds.Tables[0].Rows[0]["SanctionedAmount"].ToString() == null || ds.Tables[0].Rows[0]["SanctionedAmount"].ToString() == "")
                        objDetails.SanctionedAmount = 0;
                    else
                        objDetails.SanctionedAmount = Convert.ToDecimal(ds.Tables[0].Rows[0]["SanctionedAmount"]);

                    objDetails.Remarks = Convert.ToString(ds.Tables[0].Rows[0]["DoctorNotes"]);
                    objDetails.AuthorizedAccommodationID = Convert.ToInt64(ds.Tables[0].Rows[0]["ApprovedFacilityID"]);
                    objDetails.LetterFileName = EmailFileName;
                    objDetails.LetterPath = _SavePath;
                    objDetails.LetterCreatedDateTime = DateTime.Now;
                    if (ds.Tables[0].Rows[0]["CoPayment"].ToString() == null || ds.Tables[0].Rows[0]["CoPayment"].ToString() == "")
                        objDetails.CoPayment = 0;
                    else
                        objDetails.CoPayment = Convert.ToDecimal(ds.Tables[0].Rows[0]["CoPayment"]);
                    var result = _objMadicalScrutinyVM.Webshare_UpdateLetter_Details(objDetails, out vretunValue);

                    if (vretunValue == 1)
                    {
                        Msg += " ; Epreauth status updated";
                    }
                    else
                    {
                        Msg += " ; Epreauth status update failed";
                    }
                }
                return Msg;
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
            finally
            {
                //if (sCon.State == ConnectionState.Open)
                //    sCon.Close();
            }
        }

        public void HTMLToPdf(string HTML, string FilePath)
        {
            try
            {
                Document document = new Document();
                //string ErrorLogFilename = SharedLibrary.FileName.GetFileName(out Path, "ErrorLog", reportDate1) + ".txt"; //@"C:\Test\test.xlsx";
                //string result = "myFile_" + DateTime.Now.ToFileTime() + ".pdf";
                PdfWriter.GetInstance(document, new FileStream(FilePath, FileMode.Create));
                document.Open();
                //Image pdfImage = Image.GetInstance(Server.MapPath(""));
                //pdfImage.ScaleToFit(100, 50);
                // pdfImage.Alignment = iTextSharp.text.Image.UNDERLYING; pdfImage.SetAbsolutePosition(180, 760);
                //document.Add(pdfImage);
                iTextSharp.text.html.simpleparser.StyleSheet styles = new iTextSharp.text.html.simpleparser.StyleSheet();
                iTextSharp.text.html.simpleparser.HTMLWorker hw = new iTextSharp.text.html.simpleparser.HTMLWorker(document);
                hw.Parse(new StringReader(HTML));
                document.Close();
                //ShowPdf("Chap0101.pdf");
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
            }
        }

        //public static string CSS_STYLE = "th { background-color: #C0C0C0; font-size: 16pt; }" + "td { font-size: 10pt; }";
        //public static string HTML = "<html><body><table class='table-bordered'>"
        //                                + "<thead><tr><th>Customer Name</th><th>Customer's Address</th> </tr></thead>"
        //                                + "<tbody><tr><td> Shankar </td><td> Chennai </td></tr>"
        //                                + "<tr><td> Krishnaa </td><td> Trichy </td></tr></tbody>"
        //                                + "</table></body></html>";
        //public void GeneratePdf(String file)
        //{
        //    Document document = new Document();
        //    //PdfWriter writer = PdfWriter.GetInstance(document, new FileOutputStream(file));
        //    PdfWriter writer = PdfWriter.GetInstance(document, new FileStream(file, FileMode.Create));
        //    document.Open();

        //    ICSSResolver cssResolver = new StyleAttrCSSResolver();
        //    //ICssFile cssFile = XMLWorkerHelper.GetCSS(new ByteArrayInputStream(CSS_STYLE.getBytes()));
        //    ICssFile cssFile = XMLWorkerHelper.GetCSS(new MemoryStream(Encoding.UTF8.GetBytes(CSS_STYLE)));
        //    cssResolver.AddCss(cssFile);
        //    // HTML  
        //    HtmlPipelineContext htmlContext = new HtmlPipelineContext(null);
        //    htmlContext.SetTagFactory(Tags.GetHtmlTagProcessorFactory());
        //    // Pipelines  
        //    PdfWriterPipeline pdfFile = new PdfWriterPipeline(document, writer);
        //    HtmlPipeline html = new HtmlPipeline(htmlContext, pdfFile);
        //    CssResolverPipeline css = new CssResolverPipeline(cssResolver, html);
        //    // XML Worker  
        //    XMLWorker worker = new XMLWorker(css, true);
        //    XMLParser p = new XMLParser(worker);
        //    p.Parse(new MemoryStream(Encoding.UTF8.GetBytes(HTML)));
        //    document.Close();
        //}

        #region SaveCalculationReProcessInfo
        //************************************************************** 
        //               For Task: (SP-1103)
        //**************************************************************
        public string SaveCalculationReProcessInfo(string ClaimDetails, string Rules, Decimal DiscountByHospital, Decimal EligibleAmount, Decimal Deductible, Decimal CoPayment,
            Decimal NetEligibleAmount, Decimal Excess_SI, Decimal Excess_Preauth, Decimal ExcessPaidByPatient, Decimal AdmissibleAmount, Decimal EligiblePayableAmount,
            Decimal NegotiatedAmount, Decimal GrossAmount, Decimal TDSAmount, Decimal NetAmount, Decimal PaidByPatient, Decimal BufferUtilized, string Copayhtml,
            string ClaimUtilization, string DoctorNotes, string AdditionalNotes, bool NottoDeductFromHospital, Decimal EarlyPaymentDiscountAmount, bool AuditSkipScrutiny, Decimal PremiumAmount
           , Decimal Modularamount, Decimal Patienttobepaid, Decimal? PMTNegotiatedDiscount)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    DataTable rules = null;
                    if (Rules != "" && Rules != "[]" && Rules != null)
                    {
                        rules = (DataTable)JsonConvert.DeserializeObject(Rules, (typeof(DataTable)));
                    }
                    Newtonsoft.Json.Linq.JObject JObject = Newtonsoft.Json.Linq.JObject.Parse(ClaimDetails);
                    ClaimActionItems objActionIteams = new ClaimActionItems();
                    objActionIteams.ClaimID = Convert.ToInt64(JObject["ClaimID"]);
                    objActionIteams.Slno = Convert.ToInt16(JObject["Slno"]);
                    objActionIteams.ClaimTypeID = Convert.ToInt16(JObject["ClaimTypeID"]);
                    objActionIteams.RequestTypeID = Convert.ToInt16(JObject["RequestTypeID"]);
                    objActionIteams.ServiceTypeID = Convert.ToInt16(JObject["ServiceTypeID"]);
                    objActionIteams.ServiceSubTypeID = Convert.ToInt16(JObject["ServiceSubTypeID"]);
                    objActionIteams.ClaimStageID = Convert.ToInt32(JObject["ClaimStageID"]);
                    objActionIteams.RoleID = Convert.ToInt32(JObject["RoleID"]);
                    objActionIteams.RegionID = Convert.ToInt32(Session[Resources.SessionValue.RegionID]);
                    objActionIteams.ClaimedAmount = Convert.ToDecimal(JObject["ClaimedAmount"]);
                    objActionIteams.ClosedBy = Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]);

                    DataTable dtUtilization = null;
                    if (ClaimUtilization != "" && ClaimUtilization != "[]" && ClaimUtilization != null)
                        dtUtilization = (DataTable)JsonConvert.DeserializeObject(ClaimUtilization, (typeof(DataTable)));

                    var dt = _objMadicalScrutinyVM.SaveCalculationReProcessInfo(
                        objActionIteams, DiscountByHospital, EligibleAmount, Deductible, CoPayment, NetEligibleAmount, Excess_SI,
                        Excess_Preauth, ExcessPaidByPatient, NottoDeductFromHospital, AdmissibleAmount, EligiblePayableAmount,
                        NegotiatedAmount, GrossAmount, TDSAmount, NetAmount, PaidByPatient, Copayhtml, dtUtilization, DoctorNotes,
                        AdditionalNotes, BufferUtilized, rules, EarlyPaymentDiscountAmount, AuditSkipScrutiny, PremiumAmount, Modularamount, Patienttobepaid, PMTNegotiatedDiscount);

                    new DefaultCacheProvider().Invalidate(Convert.ToString(JObject["ClaimID"]));

                    //SP3V-2500
                    string QMS = string.Empty;
                    if (TempData["QMS"] != null)
                    {
                        QMS = TempData["QMS"].ToString();
                    }
                    //SP3V-2500
                    //SP3V-2577
                    string QMSadmin = string.Empty;
                    if (TempData["QMSadmin"] != null)
                    {
                        QMSadmin = TempData["QMSadmin"].ToString();
                    }
                    //SP3V-2577
                    //Qmsv2CMController qms = new Qmsv2CMController();
                    //qms.UpdateClaimStatus("UPDATESTATUS", "", "", "", "", QMS, "5", Session["UserRegionID"].ToString());

                    return Newtonsoft.Json.JsonConvert.SerializeObject(dt);
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }
        //**************************************************************  

        #endregion

        #region RetainSIAmountsToReserved
        //************************************************************** 
        //               For Task: (SP-1103)
        //**************************************************************
        public string RetainSIAmountsToReserved(long memberPolicyId, int siTypeId, long claimId, byte slNo)
        {
            List<object> objRes = new List<object>();
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    string msg = string.Empty;
                    bool resFlag = false;
                    int CreatedRegionId = Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]);
                    //_objMadicalScrutinyVM.RetainSIAmountsToReserved(claimId, slNo, CreatedRegionId, out resFlag, out msg);
                    DataTable dtt = _objMadicalScrutinyVM.RetainSIAmountsToReserved(claimId, slNo, CreatedRegionId, out resFlag, out msg);
                    if (resFlag == true)
                    {
                        BSIinfo objBSI = new Main().GetBSI(memberPolicyId, siTypeId, claimId, slNo);
                        //objRes.Add(new { resFlag = 1, ResponseData = JsonConvert.SerializeObject(objBSI), Message = msg });
                        objRes.Add(new { resFlag = 1, ResponseData = JsonConvert.SerializeObject(objBSI), ResponseData1 = JsonConvert.SerializeObject(dtt), Message = msg });
                    }
                    else
                    {
                        //objRes.Add(new { resFlag = 2, ResponseData = "", Message = msg });
                        objRes.Add(new { resFlag = 2, ResponseData = "", ResponseData1 = "", Message = msg });
                    }
                }
                else
                {
                    //objRes.Add(new { resFlag = 3, ResponseData = "", Message = "ErrorCode#1" });
                    objRes.Add(new { resFlag = 3, ResponseData = "", ResponseData1 = "", Message = "ErrorCode#1" });
                }
            }
            catch (Exception ex)
            {

                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                //objRes.Add(new { resFlag = 2, ResponseData = "", Message = ex.Message });
                objRes.Add(new { resFlag = 2, ResponseData = "", ResponseData1 = "", Message = "Internal server error" });
            }
            return JsonConvert.SerializeObject(objRes);
        }
        //**************************************************************  

        #endregion

        public string For_Repudiated_Insert(Int64 ClaimID, Int32 Slno, string Remarks)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    string msg;
                    Int32 Closedby = Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]);
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.For_Repudiated_Insert(ClaimID, Slno, Closedby, Remarks));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "Adjudication_Actions_Insert", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return "Internal server error";
            }
        }

        [Authorize]
        public string ReferToCRMDetails_Retrieve(long ClaimID, int SlNo)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.ReferToCRMDetails_Retrieve(ClaimID, SlNo));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "ReferInsDetails_Retrieve", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        #region Change in Json to revamp ICD master (SP-1502)
        public JsonResult GetICDInfoBasedOnParentID(long ICDParentId)
        {
            List<object> objRes = new List<object>();
            try
            {
                if (Session[SessionValue.LoginUserID] != null)
                {
                    DataTable _dtICDDetails = _objMadicalScrutinyVM.GetICDInfoBasedOnParentID(ICDParentId);
                    objRes.Add(new { Result = true, ICDInfo = _dtICDDetails, ResponseText = "Success" });
                }
                else
                {
                    objRes.Add(new { Result = false, ICDInfo = new DataTable(), ResponseText = "ErrorCode#1" });
                }
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                objRes.Add(new { Result = false, ICDInfo = new DataTable(), ResponseText = "Error occured while fetching data, Please try again" });
            }
            var jsonResult = Json(JsonConvert.SerializeObject(objRes), JsonRequestBehavior.AllowGet);
            jsonResult.MaxJsonLength = int.MaxValue;
            return jsonResult;
        }

        #endregion

        public string GeneratingApprovalLetter(string ClaimDetails, bool isApprove, string PolicyType, string MainMemberPolicyID, string PolicyID, string ProviderID,
           string BrokerID, string PayerID, string CorporateID, string InsuranceCompanyID)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    int ClaimsStageID = Convert.ToInt32(Resources.StageIDs.Audit);//24

                    string msg;
                    //  DataTable lst1 = (DataTable)JsonConvert.DeserializeObject(ClaimDetails, (typeof(DataTable)));
                    Newtonsoft.Json.Linq.JObject JObject = Newtonsoft.Json.Linq.JObject.Parse(ClaimDetails);
                    ClaimActionItems objActionIteams = new ClaimActionItems();
                    objActionIteams.ClaimID = Convert.ToInt64(JObject["ClaimID"]);
                    objActionIteams.Slno = Convert.ToInt16(JObject["Slno"]);
                    objActionIteams.ClaimTypeID = Convert.ToInt16(JObject["ClaimTypeID"]);
                    objActionIteams.RequestTypeID = Convert.ToInt16(JObject["RequestTypeID"]);
                    objActionIteams.ServiceTypeID = Convert.ToInt16(JObject["ServiceTypeID"]);
                    objActionIteams.ServiceSubTypeID = Convert.ToInt16(JObject["ServiceSubTypeID"]);
                    objActionIteams.ClaimStageID = Convert.ToInt32(JObject["ClaimStageID"]);
                    objActionIteams.RoleID = Convert.ToInt32(JObject["RoleID"]);
                    objActionIteams.RegionID = Convert.ToInt32(Session[Resources.SessionValue.RegionID]);
                    objActionIteams.ClaimedAmount = Convert.ToDecimal(JObject["ClaimedAmount"]);
                    objActionIteams.ReasonIDs_P = Convert.ToString(JObject["ReasonIDs_P"]);
                    objActionIteams.Remarks = Convert.ToString(JObject["Remarks"]);
                    objActionIteams.ClosedBy = Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]);
                    string PayeeType = Convert.ToString(JObject["PayeeType"]);

                    DataSet dsResult = _objMadicalScrutinyVM.Get_Approvalletterdata(objActionIteams.ClaimID, objActionIteams.Slno, objActionIteams.ClaimTypeID, Convert.ToInt16(PolicyType), objActionIteams.RequestTypeID);
                    if (dsResult.Tables.Count != 0)
                    {
                        VVflag = 1;
                        // ClaimAudit_Insert(ClaimDetails, isApprove, PolicyType, MainMemberPolicyID, PolicyID, ProviderID, BrokerID, PayerID, CorporateID, InsuranceCompanyID);
                        if (dsResult.Tables.Count != 0)
                        {
                            if (dsResult.Tables[0].Rows.Count > 0 && dsResult.Tables[1].Rows.Count > 0)
                            {
                                _objCommon.CommunicationInsert_Common(ref dsResult, Convert.ToInt64(JObject["ClaimID"]), Convert.ToInt32(JObject["Slno"]), Convert.ToInt64(MainMemberPolicyID),
                                    Convert.ToInt64(PolicyID), Convert.ToInt64(ProviderID), Convert.ToInt32(BrokerID == "" ? "0" : BrokerID), Convert.ToInt64(CorporateID == "" ? "0" : CorporateID),
                                    Convert.ToInt64(PayerID), Convert.ToInt32(InsuranceCompanyID), ClaimsStageID, "MedicalScrutinyController", Convert.ToInt32(Session[SessionValue.UserRegionID]), 0, Convert.ToInt32(JObject["AgentID"].ToString() == "" ? "0" : JObject["AgentID"].ToString()));

                            }
                        }
                    }
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.ClaimCommunication_Retrieve(objActionIteams.ClaimID, objActionIteams.Slno));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "ClaimAudit_Insert", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        public string GetClaimInvestigationScore(long claimid, int slno)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new MedicalScrutinyViewModel().GetClaimInvestigationScore(claimid, slno));

                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }
        public string insertDMSlog(long claimID, int slno)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    string IPAddress = "";
                    IPHostEntry Host = default(IPHostEntry);
                    string Hostname = null;
                    Hostname = System.Environment.MachineName;
                    Host = Dns.GetHostEntry(Hostname);
                    foreach (IPAddress IP in Host.AddressList)
                    {
                        if (IP.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            IPAddress = Convert.ToString(IP);
                        }
                    }
                    Int32 Createdby = Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]);
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.insertDMSlog(claimID, slno, Createdby, IPAddress));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }
        public string SaveGSTdetails(long ClaimID, int SlNo, decimal GST, decimal IGST, decimal CGST, decimal SGST)
        {
            string message = string.Empty;
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    int id = _objMadicalScrutinyVM.SaveGSTdetails(Convert.ToInt64(ClaimID), Convert.ToInt16(SlNo), GST, IGST, CGST, SGST, out message);
                    return message;
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "Get_QueryDetails", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }
        public string GeneratingSettlementLetter(string ClaimDetails, bool isApprove, string PolicyType, string MainMemberPolicyID, string PolicyID, string ProviderID,
          string BrokerID, string PayerID, string CorporateID, string InsuranceCompanyID)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    int ClaimsStageID = Convert.ToInt32(Resources.StageIDs.Settlement);//24

                    string msg;
                    //  DataTable lst1 = (DataTable)JsonConvert.DeserializeObject(ClaimDetails, (typeof(DataTable)));
                    Newtonsoft.Json.Linq.JObject JObject = Newtonsoft.Json.Linq.JObject.Parse(ClaimDetails);
                    ClaimActionItems objActionIteams = new ClaimActionItems();
                    objActionIteams.ClaimID = Convert.ToInt64(JObject["ClaimID"]);
                    objActionIteams.Slno = Convert.ToInt16(JObject["Slno"]);
                    objActionIteams.ClaimTypeID = Convert.ToInt16(JObject["ClaimTypeID"]);
                    objActionIteams.RequestTypeID = Convert.ToInt16(JObject["RequestTypeID"]);
                    objActionIteams.ServiceTypeID = Convert.ToInt16(JObject["ServiceTypeID"]);
                    objActionIteams.ServiceSubTypeID = Convert.ToInt16(JObject["ServiceSubTypeID"]);
                    objActionIteams.ClaimStageID = Convert.ToInt32(JObject["ClaimStageID"]);
                    objActionIteams.RoleID = Convert.ToInt32(JObject["RoleID"]);
                    objActionIteams.RegionID = Convert.ToInt32(Session[Resources.SessionValue.RegionID]);
                    objActionIteams.ClaimedAmount = Convert.ToDecimal(JObject["ClaimedAmount"]);
                    objActionIteams.ReasonIDs_P = Convert.ToString(JObject["ReasonIDs_P"]);
                    objActionIteams.Remarks = Convert.ToString(JObject["Remarks"]);
                    objActionIteams.ClosedBy = Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]);
                    int AgentID = Convert.ToInt32(JObject["AgentID"].ToString());
                    string PayeeType = Convert.ToString(JObject["PayeeType"]);

                    DataSet dsResult = _objMadicalScrutinyVM.Get_Settlementletterdata(objActionIteams.ClaimID, objActionIteams.Slno, objActionIteams.ClaimTypeID, Convert.ToInt16(PolicyType));
                    if (dsResult.Tables.Count != 0)
                    {
                        VVflag = 1;
                        if (dsResult.Tables.Count != 0)
                        {
                            if (dsResult.Tables[0].Rows.Count > 0 && dsResult.Tables[1].Rows.Count > 0)
                            {
                                _objCommon.CommunicationInsert_Common(ref dsResult, Convert.ToInt64(JObject["ClaimID"]), Convert.ToInt32(JObject["Slno"]), Convert.ToInt64(MainMemberPolicyID),
                                 Convert.ToInt64(PolicyID), Convert.ToInt64(ProviderID), Convert.ToInt32(BrokerID), Convert.ToInt64(CorporateID),
                                 Convert.ToInt64(PayerID), Convert.ToInt32(InsuranceCompanyID), ClaimsStageID, "MedicalScrutinyController", Convert.ToInt32(Session[SessionValue.UserRegionID]), 0, AgentID);

                            }
                        }
                    }
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.ClaimCommunication_Retrieve(objActionIteams.ClaimID, objActionIteams.Slno));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "ClaimAudit_Insert", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }
        public string checkrechagelimit(long claimID, int slno, int BPSIID, int falg)
        {
            try
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.checkrechagelimit(claimID, slno, BPSIID, falg));
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }
        public string Claimshiftingbasetopuppolicy(long claimID, int slno, long shiftingmemberID)
        {
            try
            {
                Int32 Createdby = 0;
                string msg;
                if (Session[SessionValue.UserRegionID] != null)
                {
                    Createdby = Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]);
                }
                _objMadicalScrutinyVM.Claimshiftingbasetopuppolicy(claimID, slno, shiftingmemberID, Createdby, out msg);
                return Newtonsoft.Json.JsonConvert.SerializeObject(msg);
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }
        public string GetBasetopupbalancedetails(long memberPolicyID, long Claimid, int Slno, int CoverageType)
        {
            try
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.GetBasetopupbalancedetails(memberPolicyID, Claimid, Slno, CoverageType));
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }
        public string Createtopsupertopupclaim(long ClaimID, int slno, int claimtypeID, int RequestTypeID, int ServiceTypeID, int ServiceSubTypeID, long PolicyID, int RoleID, decimal excesssuminsured
            , decimal modularamount)
        {
            try
            {
                Int32 Createdby = 0;
                long ClmID = 0;
                byte serialno;
                string msg;
                string vmessage;
                Int32 RegionID = 0;
                if (Session[SessionValue.UserRegionID] != null)
                {
                    Createdby = Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]);
                    RegionID = Convert.ToInt32(Session[Resources.SessionValue.RegionID]);
                }
                _objMadicalScrutinyVM.Createtopclaim(ClaimID, slno, claimtypeID, RequestTypeID, ServiceTypeID, ServiceSubTypeID, RoleID, Createdby, RegionID, PolicyID, excesssuminsured, modularamount, out vmessage, out ClmID, out serialno);
                return Newtonsoft.Json.JsonConvert.SerializeObject(vmessage);
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }
        //Added by Rajesh Yerramsetti
        [Authorize]
        public string ReviewedRemarksDetails_Retrieve(long ClaimID, int SlNo)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.ReviewedRemarksDetails_Retrieve(ClaimID, SlNo));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "ReferInsDetails_Retrieve", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        public string ReturnReviewedRemarksDetails_Retrieve(long ClaimID, int SlNo)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.ReturnReviewedRemarksDetails_Retrieve(ClaimID, SlNo));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "ReferInsDetails_Retrieve", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        public string Submit_RequestReviewed_Insert(string ClaimDetails)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {

                    string msg = "";
                    byte? MainSlno = 1;
                    //Session[SessionValue.UserRegionID] = 1;
                    ClaimActionItems objActionIteams = new ClaimActionItems();
                    Newtonsoft.Json.Linq.JObject JObject = Newtonsoft.Json.Linq.JObject.Parse(ClaimDetails);
                    Int64 ClaimID = Convert.ToInt64(JObject["ClaimID"]);
                    Int16 Slno = Convert.ToInt16(JObject["Slno"]);
                    Int32 VReviewedflag = Convert.ToInt32(JObject["ReviewedFlag"]);
                    if (VReviewedflag == 2)
                    {
                        string CreateEnhancement = CreateEnhancementRequest(ClaimDetails, MainSlno, "", 0, 0);
                        Newtonsoft.Json.Linq.JObject JObjectt = Newtonsoft.Json.Linq.JObject.Parse(CreateEnhancement);
                        if (JObjectt["ID"].ToString() != "0")
                        {
                            ClaimID = Convert.ToInt64(JObjectt["ID"]);
                            Slno = 2;
                            msg = JObjectt["Name"].ToString();
                        }
                        else
                        {
                            ClaimID = 0;
                            msg = JObjectt["Name"].ToString();
                        }
                    }
                    if (ClaimID != 0)
                    {
                        objActionIteams.ClaimID = ClaimID;
                        objActionIteams.Slno = Slno;
                        objActionIteams.ClaimTypeID = Convert.ToInt16(JObject["ClaimTypeID"]);
                        objActionIteams.RequestTypeID = Convert.ToInt16(JObject["RequestTypeID"]);
                        objActionIteams.ServiceTypeID = Convert.ToInt16(JObject["ServiceTypeID"]);
                        objActionIteams.ServiceSubTypeID = Convert.ToInt16(JObject["ServiceSubTypeID"]);
                        objActionIteams.ClaimStageID = Convert.ToInt32(JObject["ClaimStageID"]);
                        objActionIteams.RoleID = Convert.ToInt32(JObject["RoleID"]);
                        objActionIteams.RegionID = Convert.ToInt32(Session[Resources.SessionValue.RegionID]);
                        objActionIteams.ClaimedAmount = Convert.ToDecimal(JObject["ClaimedAmount"]);
                        objActionIteams.ReasonIDs_P = Convert.ToString(JObject["ReasonIDs_P"]);
                        objActionIteams.Remarks = Convert.ToString(JObject["Remarks"]);
                        objActionIteams.ClosedBy = Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]);
                        //_objMadicalScrutinyVM.Adjudication_Actions_Insert(objActionIteams, out msg);
                        objActionIteams.ReviewedFlag = Convert.ToInt32(JObject["ReviewedFlag"]);

                        //SP3V-251 - Requirement to create only an SMS template for Kotak in Claim Investigation stage
                        DataSet dsResult = null;
                        msg = "";
                        dsResult = _objMadicalScrutinyVM.Submit_RequestReviewed_Insert(objActionIteams, out msg);
                    }
                    return Newtonsoft.Json.JsonConvert.SerializeObject(msg);

                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "Adjudication_Actions_Insert", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        public string CreateEnhancementRequest(string values, byte? MainSlno, string epreauthValues, Int64? EEARequestID, Int64? EEAAuthorizedID)
        {
            string vMessage = string.Empty;
            long ClaimId;

            string Msg = string.Empty;
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    int UserregionId = Convert.ToInt32(Session[SessionValue.UserRegionID]);
                    if (UserregionId != 0)
                    {
                        long clmID = 0;
                        DataSet ds = null;
                        PAClaimRequest request = (PAClaimRequest)JsonConvert.DeserializeObject(values, (typeof(PAClaimRequest)));
                        request.RoleID = 12;
                        request.Flag = 1;
                        int RegionId = Convert.ToInt32(Session[SessionValue.RegionID]);
                        request.RegionID = RegionId;
                        request.ReceivedDate = DateTime.Now;
                        request.claimdiagnosis = 0;
                        request.StageID = 3;
                        request.RequestTypeID = 2;
                        request.isfarceClaim = false;
                        request.Mobile = "";
                        request.Email = "";
                        request.DocumentRemarks = "";
                        request.ReOpenRemarks = "";
                        request.ProbableDiagnosis = "";
                        request.IsCovidClaim = false;
                        request.IsClaimForClosure = 0;

                        ds = _objClaimsVM.InsertPreauthRequest(ref request, ref UserregionId, MainSlno, out ClaimId, out Msg, out this.Slno, Convert.ToInt32(request.CorpID));
                        MappingTableLong _res = new MappingTableLong();
                        _res.ID = ClaimId;
                        _res.Name = Msg;
                        StageId = request.StageID;
                        if (ClaimId != 0 && request.ReceivedMode_P23 == 87 && (request.RequestTypeID == 2 || request.RequestTypeID == 3))
                        {
                            string Message = string.Empty;
                            var req = _objClaimsVM.InsertEpreAuthStatus(ref request, ClaimId, Slno, 2, EEARequestID, EEAAuthorizedID, out Message);
                            clmID = ClaimId;
                        }
                        if (ClaimId != 0)
                            clmID = ClaimId;
                        else
                            clmID = Convert.ToInt64(request.ClaimID);

                        if (clmID != 0 && request.RequestTypeID != 5)
                        {
                            if (ds.Tables.Count != 0)
                            {
                                if (ds.Tables[0].Rows.Count > 0 && ds.Tables[1].Rows.Count > 0)
                                {
                                    //CommunicatingPreauthRequest(ref ds, ClaimId, request.MemberPolicyID, request.PolicyID, request.ProviderID, request.BrokerID, request.CorpID, request.PayerID, request.IssueID);
                                    _objCommon.CommunicationInsert_Common(ref ds, clmID, Slno, request.MemberPolicyID, request.PolicyID, request.ProviderID, request.BrokerID,
                                        request.CorpID, request.PayerID, request.IssueID, 3, "ClaimsController", Convert.ToInt32(Session[SessionValue.UserRegionID]), 0, request.AgentID);
                                }
                            }
                        }
                        return Newtonsoft.Json.JsonConvert.SerializeObject(_res);
                    }
                    else
                    {
                        return "ErrorCode#1";
                    }
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {

                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }


        #region DMS Documents
        //[RoleAuthorize(12, 20, 13, 14, 15, 16, 17, 18, 19, 32, 21)]
        [HttpGet]
        public async Task<ActionResult> GetDMSDocuments(string Q)//string claimid, string slNo)
        {

            DMS_ClaimDocumentListViewModel lstClaimsDocs = new DMS_ClaimDocumentListViewModel();
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            string clientId = ConfigurationManager.AppSettings["ClientID"];
            var res = new ApiResponse<int>();
            try
            {
                if (string.IsNullOrWhiteSpace(Q))
                {
                    if (Request.IsAjaxRequest())
                    {
                        // For AJAX call — return JSON
                        res.Success = false;
                        res.Message = "Invalid parameter: Q cannot be null or empty.";
                        return Json(res, JsonRequestBehavior.AllowGet);
                    }
                    else
                    {
                        // For browser URL access — redirect
                        return RedirectToAction("MethodNotFound", "Account");
                    }
                }
                string encryptionkey = "";
                DataTable URLEncrytKey = new CommonViewModel().getencryptionkey();
                if (URLEncrytKey.Rows.Count > 0)
                    encryptionkey = URLEncrytKey.Rows[0]["PrivateKey"].ToString();

                string payerDetails = new MasterUtilsBL().Decrypt(Q, encryptionkey);
                var parts = payerDetails.Split('|');
                if (parts.Length < 2)
                    throw new ArgumentException("Invalid decrypted parameter format.");

                string claimid = parts[0].ToString();
                string slNo = parts[1].ToString();


                string Baseurl = ConfigurationManager.AppSettings["DMSApiURL"];
                using (var client = new HttpClient())
                {
                    string dmsToken = string.Empty;
                    //DMS Generate Token
                    {
                        string DMSAPIKey = ConfigurationManager.AppSettings["DMSAPIKey"];
                        string clearText = clientId + "|" + DMSAPIKey;
                        string encryptedString = new MasterUtilsBL().Encrypt(clearText, Convert.ToString(ConfigurationManager.AppSettings["URLEncryptionKey"]));
                        var request = new HttpRequestMessage(HttpMethod.Get, Baseurl + "api/auth/keyauthentication?q=" + WebUtility.UrlEncode(encryptedString));
                        HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                        if (response.IsSuccessStatusCode)
                        {
                            dmsToken = response.Content.ReadAsStringAsync().Result.ToString();
                        }
                    }

                    McarePlusEntities DB = new McarePlusEntities();

                    var listRoles = DB.Lnk_UserRoles.AsEnumerable().Where(a => a.UserID == Convert.ToInt32(Session[SessionValue.LoginUserID]) && a.Deleted == false).Select(a => a.RoleID).Distinct();
                    string roleIds = string.Join(",", listRoles);
                    var claimDocsRequest = new HttpRequestMessage(HttpMethod.Get, Baseurl + "api/Document/claimdocslistview?claimId=" + claimid + "&extNo=" + 0 + "&clientID=" + clientId + "&roleIds=" + roleIds);
                    claimDocsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", dmsToken);
                    HttpResponseMessage Docsresponse = await client.SendAsync(claimDocsRequest, HttpCompletionOption.ResponseHeadersRead);

                    if (Docsresponse.IsSuccessStatusCode)
                    {
                        var docsResult = Docsresponse.Content.ReadAsStringAsync().Result;
                        lstClaimsDocs = JsonConvert.DeserializeObject<DMS_ClaimDocumentListViewModel>(docsResult);

                        IList<DMS_DocumentListViewModel> templstClaimsDocs = new ObservableCollection<DMS_DocumentListViewModel>();
                        string clearText = Session[SessionValue.LoginUserID] + "|true";
                        string encryptString = new MasterUtilsBL().Encrypt(clearText, Convert.ToString(ConfigurationManager.AppSettings["URLEncryptionKey"]));
                        encryptString = Url.Encode(encryptString);
                        if (lstClaimsDocs != null && lstClaimsDocs.DocumentsListViewDto.Count > 0)
                        {
                            foreach (var item in lstClaimsDocs.DocumentsListViewDto)
                            {
                                item.NextGenViewerURL += "&v=" + encryptString;
                                item.NextGenAnnotationURL += "&v=" + encryptString;
                                //item.NextGenViewerURL += "&userId=" + Session[SessionValue.LoginUserID];
                                //item.NextGenAnnotationURL += "&userId=" + Session[SessionValue.LoginUserID];
                                templstClaimsDocs.Add(item);
                            }
                            lstClaimsDocs.DocumentsListViewDto = templstClaimsDocs;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
            }

            //return Newtonsoft.Json.JsonConvert.SerializeObject(lstClaimsDocs);
            return this.Json(JsonConvert.SerializeObject(lstClaimsDocs, Newtonsoft.Json.Formatting.Indented), JsonRequestBehavior.AllowGet);

        }
        public async Task<ActionResult> GetDocumentCategories()
        {
            string Baseurl = ConfigurationManager.AppSettings["DMSApiURL"];
            List<DMS_DocCategoriesModel> lstDocCategories = new List<DMS_DocCategoriesModel>();
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            string clientId = ConfigurationManager.AppSettings["ClientID"];
            try
            {
                using (var client = new HttpClient())
                {
                    string dmsToken = string.Empty;
                    //DMS Generate Token
                    {
                        string DMSAPIKey = ConfigurationManager.AppSettings["DMSAPIKey"];
                        string clearText = clientId + "|" + DMSAPIKey;
                        string encryptedString = new MasterUtilsBL().Encrypt(clearText, Convert.ToString(ConfigurationManager.AppSettings["URLEncryptionKey"]));
                        var request = new HttpRequestMessage(HttpMethod.Get, Baseurl + "api/auth/keyauthentication?q=" + WebUtility.UrlEncode(encryptedString));
                        HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                        if (response.IsSuccessStatusCode)
                        {
                            dmsToken = response.Content.ReadAsStringAsync().Result.ToString();
                        }
                    }

                    var docRequests = new HttpRequestMessage(HttpMethod.Get, Baseurl + "api/Document/getdoccategories");
                    docRequests.Headers.Authorization = new AuthenticationHeaderValue("Bearer", dmsToken);
                    HttpResponseMessage Docsresponse = await client.SendAsync(docRequests, HttpCompletionOption.ResponseHeadersRead);

                    if (Docsresponse.IsSuccessStatusCode)
                    {
                        var docsResult = Docsresponse.Content.ReadAsStringAsync().Result;
                        lstDocCategories = JsonConvert.DeserializeObject<List<DMS_DocCategoriesModel>>(docsResult);
                    }
                }
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
            }

            return this.Json(JsonConvert.SerializeObject(lstDocCategories, Newtonsoft.Json.Formatting.Indented), JsonRequestBehavior.AllowGet);

        }
        public async Task<ActionResult> GetDocumentSubCategories()
        {
            string Baseurl = ConfigurationManager.AppSettings["DMSApiURL"];
            List<DMS_DocSubCategoriesModel> lstDocSubCategories = new List<DMS_DocSubCategoriesModel>();
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            string clientId = ConfigurationManager.AppSettings["ClientID"];
            try
            {
                using (var client = new HttpClient())
                {
                    string dmsToken = string.Empty;
                    //DMS Generate Token
                    {
                        string DMSAPIKey = ConfigurationManager.AppSettings["DMSAPIKey"];
                        string clearText = clientId + "|" + DMSAPIKey;
                        string encryptedString = new MasterUtilsBL().Encrypt(clearText, Convert.ToString(ConfigurationManager.AppSettings["URLEncryptionKey"]));
                        var request = new HttpRequestMessage(HttpMethod.Get, Baseurl + "api/auth/keyauthentication?q=" + WebUtility.UrlEncode(encryptedString));
                        HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                        if (response.IsSuccessStatusCode)
                        {
                            dmsToken = response.Content.ReadAsStringAsync().Result.ToString();
                        }
                    }

                    var docRequests = new HttpRequestMessage(HttpMethod.Get, Baseurl + "api/Document/subcategories");
                    docRequests.Headers.Authorization = new AuthenticationHeaderValue("Bearer", dmsToken);
                    HttpResponseMessage Docsresponse = await client.SendAsync(docRequests, HttpCompletionOption.ResponseHeadersRead);

                    if (Docsresponse.IsSuccessStatusCode)
                    {
                        var docsResult = Docsresponse.Content.ReadAsStringAsync().Result;
                        lstDocSubCategories = JsonConvert.DeserializeObject<List<DMS_DocSubCategoriesModel>>(docsResult);
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return this.Json(JsonConvert.SerializeObject(lstDocSubCategories, Newtonsoft.Json.Formatting.Indented), JsonRequestBehavior.AllowGet);

        }
        //SP3V-411 SP3V-1058 Leena
        public string GetProviderServicePackageDisc(long ClaimID, long ProviderID, String MouId, bool isFrmArchived = false)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.GetProviderServicePackageDisc(ClaimID, ProviderID, Convert.ToInt64(MouId), isFrmArchived));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }

        }
        //END SP3V-411 SP3V-1058 Leena

        public string GetClaimTypeRequestType(long ClaimID, int slNo)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.GetClaimTypeRequestType(ClaimID, slNo));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }

        }


        #endregion

        #region Bima Satark Details
        [HttpPost]
        public JsonResult GetBimaSatarkDetails(string Action, string claimdetailsid)
        {
            try
            {
                if (Session[SessionValue.RegionID] != null)
                {
                    DataSet dt = new DataSet();
                    dt = _objMadicalScrutinyVM.GetBimaSatarkDetails(Action, claimdetailsid);
                    return this.Json(JsonConvert.SerializeObject(dt, Newtonsoft.Json.Formatting.Indented), JsonRequestBehavior.AllowGet);
                }
                else
                {
                    return this.Json(JsonConvert.SerializeObject("ErrorCode#1", Newtonsoft.Json.Formatting.Indented), JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return this.Json(JsonConvert.SerializeObject(ex.Message, Newtonsoft.Json.Formatting.Indented), JsonRequestBehavior.AllowGet);
            }
        }
        [HttpPost]
        public JsonResult SaveBimaSatarkInformation(string claimdetailsid, string ClaimID)
        {
            try
            {
                DataTable data = GetBimaSatarkDatatable();
                data.Rows.Add(new Object[]{
                 Request.Form["ClaimDetailsId"].ToString().Trim(),
                 Request.Form["ClaimId"].ToString().Trim(),
                 Request.Form["txtSRF_ID_on_Covid_Report"].ToString().Trim(),
                 Request.Form["txtICMR_ID_on_Covid_Report"].ToString().Trim(),
                 Request.Form["Radiologist_Name_1"].ToString().Trim(),
                 Request.Form["txtRadiologist_Address_1"].ToString().Trim(),
                 Request.Form["txtRadiologist_Pin_Code_1"].ToString().Trim(),
                 Request.Form["txtRadiologist_Registration_number_1"].ToString().Trim(),
                 Request.Form["txtIRadiologist_email_Id_1"].ToString().Trim(),
                 Request.Form["txtRadiologist_Phone_Number_1"].ToString().Trim(),
                 Request.Form["txtRadiologist_Longitude_1"].ToString().Trim(),
                 Request.Form["txtRadiologist_Latitude_1"].ToString().Trim(),
                 Request.Form["txtRadiologist_Name_2"].ToString().Trim(),
                 Request.Form["txtRadiologist_Address_2"].ToString().Trim(),
                 Request.Form["txtRadiologist_Pin_Code_2"].ToString().Trim(),
                 Request.Form["txtRadiologist_Registration_number_2"].ToString().Trim(),
                 Request.Form["txtIRadiologist_email_Id_2"].ToString().Trim(),
                 Request.Form["txtRadiologist_Phone_Number_2"].ToString().Trim(),
                 Request.Form["txtRadiologist_Longitude_2"].ToString().Trim(),
                 Request.Form["txtRadiologist_Latitude_2"].ToString().Trim(),
                 Request.Form["Pathologist_Name_1"].ToString().Trim(),
                 Request.Form["txtPathologist_Address_1"].ToString().Trim(),
                 Request.Form["txtPathologist_Pin_Code_1"].ToString().Trim(),
                 Request.Form["txtPathologist_Registration_number_1"].ToString().Trim(),
                 Request.Form["txtPathologist_email_Id_1"].ToString().Trim(),
                 Request.Form["txtPathologist_Phone_Number_1"].ToString().Trim(),
                 Request.Form["txtPathologist_Longitude_1"].ToString().Trim(),
                 Request.Form["txtPathologist_Latitude_1"].ToString().Trim(),
                 Request.Form["txtPathologist_Name_2"].ToString().Trim(),
                 Request.Form["txtPathologist_Address_2"].ToString().Trim(),
                 Request.Form["txtPathologist_Pin_Code_2"].ToString().Trim(),
                 Request.Form["txtPathologist_Registration_number_2"].ToString().Trim(),
                 Request.Form["txtPathologist_email_Id_2"].ToString().Trim(),
                 Request.Form["txtPathologist_Phone_Number_2"].ToString().Trim(),
                 Request.Form["txtPathologist_Longitude_2"].ToString().Trim(),
                 Request.Form["txtPathologist_Latitude_2"].ToString().Trim(),
                 Request.Form["txtTreating_Doctor_PAN_Card"].ToString().Trim(),
                // Request.Form["txtCorp_Buff_auth_name"].ToString().Trim(),
                 //Request.Form["txtCorp_Buff_auth_desig"].ToString().Trim(),
                 Request.Form["txtPhysicianName"].ToString().Trim(),
                 Request.Form["txtPhysicianMobileNo"].ToString().Trim(),
                 Request.Form["txtPhysicianRegNo"].ToString().Trim(),
                 Request.Form["txtradio"].ToString().Trim(),
                  Request.Form["txtradio1"].ToString().Trim(),
                   Request.Form["txtradio2"].ToString().Trim(),
                    Request.Form["txtradio3"].ToString().Trim()
           });
                string xmlString = string.Empty;
                using (TextWriter writer = new StringWriter())
                {
                    data.WriteXml(writer);
                    xmlString = writer.ToString();
                }

                DataSet dt = new DataSet();
                dt = _objMadicalScrutinyVM.GetBimaSatarkDetails(Request.Form["Action"].ToString().Trim(), Request.Form["ClaimDetailsId"].ToString().Trim(), xmlString, Session["UserRegionID"].ToString(), Request.Form["ClaimId"].ToString().Trim());
                return this.Json(JsonConvert.SerializeObject(dt.Tables[0], Newtonsoft.Json.Formatting.Indented), JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
                return this.Json(JsonConvert.SerializeObject(null, Newtonsoft.Json.Formatting.Indented), JsonRequestBehavior.AllowGet);
            }

        }

        public DataTable GetBimaSatarkDatatable()
        {
            DataTable data = new System.Data.DataTable("BimaSatark");
            data.Columns.Add("ClaimDetailsID");
            data.Columns.Add("ClaimId");
            data.Columns.Add("txtSRF_ID_on_Covid_Report");
            data.Columns.Add("txtICMR_ID_on_Covid_Report");
            data.Columns.Add("Radiologist_Name_1");
            data.Columns.Add("txtRadiologist_Address_1");
            data.Columns.Add("txtRadiologist_Pin_Code_1");
            data.Columns.Add("txtRadiologist_Registration_number_1");
            data.Columns.Add("txtIRadiologist_email_Id_1");
            data.Columns.Add("txtRadiologist_Phone_Number_1");
            data.Columns.Add("txtRadiologist_Longitude_1");
            data.Columns.Add("txtRadiologist_Latitude_1");
            data.Columns.Add("txtRadiologist_Name_2");
            data.Columns.Add("txtRadiologist_Address_2");
            data.Columns.Add("txtRadiologist_Pin_Code_2");
            data.Columns.Add("txtRadiologist_Registration_number_2");
            data.Columns.Add("txtIRadiologist_email_Id_2");
            data.Columns.Add("txtRadiologist_Phone_Number_2");
            data.Columns.Add("txtRadiologist_Longitude_2");
            data.Columns.Add("txtRadiologist_Latitude_2");
            data.Columns.Add("Pathologist_Name_1");
            data.Columns.Add("txtPathologist_Address_1");
            data.Columns.Add("txtPathologist_Pin_Code_1");
            data.Columns.Add("txtPathologist_Registration_number_1");
            data.Columns.Add("txtPathologist_email_Id_1");
            data.Columns.Add("txtPathologist_Phone_Number_1");
            data.Columns.Add("txtPathologist_Longitude_1");
            data.Columns.Add("txtPathologist_Latitude_1");
            data.Columns.Add("txtPathologist_Name_2");
            data.Columns.Add("txtPathologist_Address_2");
            data.Columns.Add("txtPathologist_Pin_Code_2");
            data.Columns.Add("txtPathologist_Registration_number_2");
            data.Columns.Add("txtPathologist_email_Id_2");
            data.Columns.Add("txtPathologist_Phone_Number_2");
            data.Columns.Add("txtPathologist_Longitude_2");
            data.Columns.Add("txtPathologist_Latitude_2");
            data.Columns.Add("txtTreating_Doctor_PAN_Card");
            //data.Columns.Add("txtCorp_Buff_auth_name");
            //data.Columns.Add("txtCorp_Buff_auth_desig");
            data.Columns.Add("txtPhysicianName");
            data.Columns.Add("txtPhysicianMobileNo");
            data.Columns.Add("txtPhysicianRegNo");
            data.Columns.Add("txtradio");
            data.Columns.Add("txtradio1");
            data.Columns.Add("txtradio2");
            data.Columns.Add("txtradio3");
            return data;
        }

        #endregion

        [HttpPost]
        public JsonResult SaveClaimProviderDetails(string ClaimID, string ProviderID)
        {
            try
            {
                if (Session[SessionValue.RegionID] != null)
                {
                    string Msg = string.Empty;

                    DataTable filesdt = null;
                    filesdt = _objMadicalScrutinyVM.SaveClaimProviderDetails(ClaimID, ProviderID);
                    return this.Json(JsonConvert.SerializeObject(filesdt, Newtonsoft.Json.Formatting.Indented), JsonRequestBehavior.AllowGet);

                }
                else
                {
                    return this.Json(JsonConvert.SerializeObject("ErrorCode#1", Newtonsoft.Json.Formatting.Indented), JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return this.Json(JsonConvert.SerializeObject(ex.Message, Newtonsoft.Json.Formatting.Indented), JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public string SaveClaimOptionalCovers(string Action, Int64? CLAIMID, int SLNO, Int64 MemberPolicyId, string Optional_Cover_Amount = "", string Optional_Cover_Utilized = "", string Optional_Cover_blocked = "", Int32? OptionalCoverID = 0, Int64? COCID = 0, string claimTypeID = "")
        {
            try
            {
                if (Session[SessionValue.RegionID] != null)
                {
                    string Msg = string.Empty;

                    DataSet dsBpData = _objMadicalScrutinyVM.SaveClaimOptionalCovers(Action, CLAIMID, SLNO, MemberPolicyId, Optional_Cover_Amount, Optional_Cover_Utilized, Optional_Cover_blocked, Convert.ToInt32(Session[SessionValue.UserRegionID]), OptionalCoverID, COCID, claimTypeID);
                    return Newtonsoft.Json.JsonConvert.SerializeObject(dsBpData);

                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }
        public string validateIRApprove(long ClaimID, int Slno)
        {
            string msg;
            try
            {
                if (Session[SessionValue.RegionID] != null)
                {
                    _objMadicalScrutinyVM.validateIRApprove(ClaimID, Slno, out msg);
                    return Newtonsoft.Json.JsonConvert.SerializeObject(msg);

                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        public string SaveBillingAmount(string ClaimDetails, string Rules, Decimal DiscountByHospital, Decimal EligibleAmount, Decimal Deductible, Decimal CoPayment,
            Decimal NetEligibleAmount, Decimal Excess_SI, Decimal Excess_Preauth, Decimal ExcessPaidByPatient, Decimal AdmissibleAmount, Decimal EligiblePayableAmount,
            Decimal NegotiatedAmount, Decimal GrossAmount, Decimal TDSAmount, Decimal NetAmount, Decimal PaidByPatient, Decimal BufferUtilized, string Copayhtml, string ClaimUtilization, string DoctorNotes, string AdditionalNotes, bool NottoDeductFromHospital, Decimal EarlyPaymentDiscountAmount, bool SkipScrutiny, Decimal PremiumDeducted,
            string QMSID, string QMSAdminID, Decimal Modularamount, Decimal Patienttobepaid)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {

                    string QMS = string.Empty;
                    QMS = QMSID;
                    string QMSadmin = string.Empty;
                    QMSadmin = QMSAdminID;

                    string msg;
                    //DataTable lst = new DataTable("Something1");
                    //lst.TableName = "Something1";
                    DataTable rules = null;
                    if (Rules != "" && Rules != "[]" && Rules != null)
                        rules = (DataTable)JsonConvert.DeserializeObject(Rules, (typeof(DataTable)));

                    //  DataTable lst1 = (DataTable)JsonConvert.DeserializeObject(ClaimDetails, (typeof(DataTable)));
                    Newtonsoft.Json.Linq.JObject JObject = Newtonsoft.Json.Linq.JObject.Parse(ClaimDetails);
                    ClaimActionItems objActionIteams = new ClaimActionItems();
                    objActionIteams.ClaimID = Convert.ToInt64(JObject["ClaimID"]);
                    objActionIteams.Slno = Convert.ToInt16(JObject["Slno"]);
                    objActionIteams.ClaimTypeID = Convert.ToInt16(JObject["ClaimTypeID"]);
                    objActionIteams.RequestTypeID = Convert.ToInt16(JObject["RequestTypeID"]);
                    objActionIteams.ServiceTypeID = Convert.ToInt16(JObject["ServiceTypeID"]);
                    objActionIteams.ServiceSubTypeID = Convert.ToInt16(JObject["ServiceSubTypeID"]);
                    objActionIteams.ClaimStageID = Convert.ToInt32(JObject["ClaimStageID"]);
                    objActionIteams.RoleID = Convert.ToInt32(JObject["RoleID"]);
                    objActionIteams.RegionID = Convert.ToInt32(Session[Resources.SessionValue.RegionID]);
                    objActionIteams.ClaimedAmount = Convert.ToDecimal(JObject["ClaimedAmount"]);
                    objActionIteams.ClosedBy = Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]);

                    DataTable dtUtilization = null;
                    if (ClaimUtilization != "" && ClaimUtilization != "[]" && ClaimUtilization != null)
                        dtUtilization = (DataTable)JsonConvert.DeserializeObject(ClaimUtilization, (typeof(DataTable)));

                    _objMadicalScrutinyVM.UpdateCalculatedBill(objActionIteams, rules, DiscountByHospital, EligibleAmount, Deductible, CoPayment, NetEligibleAmount, Excess_SI, Excess_Preauth, ExcessPaidByPatient, NottoDeductFromHospital, AdmissibleAmount, EligiblePayableAmount, NegotiatedAmount, GrossAmount, TDSAmount, NetAmount, PaidByPatient, Copayhtml, dtUtilization, DoctorNotes, AdditionalNotes, BufferUtilized, EarlyPaymentDiscountAmount, SkipScrutiny, PremiumDeducted, Modularamount, Patienttobepaid, out msg);

                    //Claim Lock Release Code By Srinu B
                    new DefaultCacheProvider().Invalidate(Convert.ToString(JObject["ClaimID"]));
                    Qmsv2CMController qms = new Qmsv2CMController();
                    qms.UpdateClaimStatus("UPDATESTATUS", "", "", "", "", QMS, "5", Session["UserRegionID"].ToString());
                    return Newtonsoft.Json.JsonConvert.SerializeObject(msg);

                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //    _objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "ClaimRules_Insert", Session[Resources.SessionValue.LoginUserID].ToString());
                //    throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }
        public DataTable ToDataTable<T>(List<T> items)
        {
            DataTable dataTable = new DataTable(typeof(T).Name);
            if (items == null || items.Count == 0) return dataTable;
            //Get all the properties
            PropertyInfo[] Props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (PropertyInfo prop in Props)
            {
                //Setting column names as Property names
                dataTable.Columns.Add(prop.Name);
            }
            foreach (T item in items)
            {
                var values = new object[Props.Length];
                for (int i = 0; i < Props.Length; i++)
                {
                    //inserting property values to datatable rows
                    values[i] = Props[i].GetValue(item, null);
                }
                if (Convert.ToInt32(values[1]) > 0 || Convert.ToInt32(values[2]) > 0 || Convert.ToInt32(values[3]) > 0 || Convert.ToInt32(values[4]) > 0)
                {
                    dataTable.Rows.Add(values);
                }
            }
            //put a breakpoint here and check datatable
            return dataTable;
        }

        //[RoleAuthorize(12, 20, 13, 14, 15, 16, 17, 18, 19, 32, 21)]
        [HttpGet]
        public ActionResult GetHoapitalpasthistory(string Q)
        {
            var response = new ApiResponse<string>();
            try
            {
                if (string.IsNullOrWhiteSpace(Q))
                {
                    if (Request.IsAjaxRequest())
                    {
                        // For AJAX call — return JSON
                        response.Success = false;
                        response.Message = "Invalid parameter: Q cannot be null or empty.";
                        return Json(response, JsonRequestBehavior.AllowGet);
                    }
                    else
                    {
                        // For browser URL access — redirect
                        return RedirectToAction("MethodNotFound", "Account");
                    }
                }
                string encryptionkey = "";
                DataTable URLEncrytKey = new CommonViewModel().getencryptionkey();
                if (URLEncrytKey.Rows.Count > 0)
                    encryptionkey = URLEncrytKey.Rows[0]["PrivateKey"].ToString();

                string payerDetails = new MasterUtilsBL().Decrypt(Q, encryptionkey);
                var parts = payerDetails.Split('|');
                if (parts.Length < 2)
                    throw new ArgumentException("Invalid decrypted parameter format.");

                string claimID = parts[0].ToString();
                string slno = parts[1].ToString();
                response.Data = Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.GetHospitalpasthistory(Convert.ToInt64(claimID), Convert.ToInt32(slno)));

                response.Success = true;
                return Json(response, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                response.Success = false;
                response.Message = "An error occurred while processing the request.";
                response.ErrorCode = "INS001";
                return Json(response, JsonRequestBehavior.AllowGet);
            }
        }
        public string GetUCRData(string cityID, string HospitalCategory_P68, string claimID, string claimtypeID, string Probable_line_treatment, string RoomType, string providerID, string level3)
        {
            try
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.GetUCRData(Convert.ToInt32(cityID), Convert.ToInt32(HospitalCategory_P68), Convert.ToInt64(claimID), Convert.ToInt32(claimtypeID), Convert.ToInt32(Probable_line_treatment)
                    , Convert.ToInt32(providerID), Convert.ToInt32(level3), RoomType));
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        public string ITGI_RefertoInsurerAtForAuditStage(long ClaimID, int slno)
        {
            string msg = string.Empty;
            try
            {
                var insurerresobj = new refertoinsresponse();
                string token = Convert.ToString(ConfigurationManager.AppSettings["ITGItoken"]);// "RmhwbEtleTpwc2RmZyRqa2wzNDU=";
                var client = new HttpClient();
                string apiUrl = Convert.ToString(ConfigurationManager.AppSettings["ITGIAPIurlForAuditTOReferToInsuer"]); //"https://uat-spectra.fhpl.net/api/ITIC/SpectraClaimServicesPush";
                refertoinsurerAPIRequest docReq = new refertoinsurerAPIRequest();
                docReq.ClaimID = ClaimID;
                docReq.Slno = Slno;
                var jsonDoc = JsonConvert.SerializeObject(docReq).ToString();
                var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
                request.Headers.Add("Authorization", "Basic " + token);
                var content = new StringContent(jsonDoc.ToString(), null, "application/json");
                request.Content = content;
                var response = client.SendAsync(request);
                var insurerresponse = response.Result.Content.ReadAsStringAsync().Result;
                insurerresobj = JsonConvert.DeserializeObject<refertoinsresponse>(insurerresponse);
                msg = "Status Code" + " " + insurerresobj.StatusCode + " " + insurerresobj.Message;
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                msg = "Status Code" + " " + 100 + " " + "Internal server, please contact admin";
            }
            return msg;
        }

        public bool GetIsRestrictedPolicyNo(string policyID, int issueId)
        {
            bool result = false;
            try
            {
                DataTable dtPolicy = _objMadicalScrutinyVM.GetPolicyDetailsbyPolicyId(policyID);
                string iTGIRestrictedPolicyNumbers = Convert.ToString(ConfigurationManager.AppSettings["ITGIRestrictedPolicyNumbers"]);
                if (!string.IsNullOrEmpty(iTGIRestrictedPolicyNumbers) && dtPolicy.Rows.Count > 0)
                {
                    string[] urlList = iTGIRestrictedPolicyNumbers.Split(',');
                    foreach (var prefix in urlList)
                    {
                        if (dtPolicy.Rows[0]["PolicyNo"].ToString().StartsWith(prefix))
                        {
                            result = true;
                            return result;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
            }
            return result;
        }

        [Authorize]
        public string ClearUserClaimCache()
        {
            new DefaultCacheProvider().Invalidate(Convert.ToInt32(Session[SessionValue.LoginUserID]), CacheItemType.ClaimLock);
            return "Sucess";
        }

        public string SAA_Submit_RequestReviewed_Insert(Int64 ClaimID, Int32 Slno, Int32 flag, string Remarks)
        {
            string msg = "";
            DataSet dsResult;
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    Int32 Createdby = Convert.ToInt32(Session[Resources.SessionValue.UserRegionID]);
                    dsResult = _objMadicalScrutinyVM.SAA_Submit_RequestReviewed_Insert(ClaimID, Slno, flag, Remarks, Createdby, out msg);
                    if (flag == 1)
                    {
                        DataTable dt = _objMadicalScrutinyVM.Getcommuicationbasic_details(ClaimID, Slno);
                        if (dsResult.Tables.Count != 0 && dt.Rows.Count > 0)
                        {
                            if (Convert.ToInt16(dt.Rows[0]["RequesttypeID"].ToString()) == 1 || Convert.ToInt16(dt.Rows[0]["RequesttypeID"].ToString()) == 2 || Convert.ToInt16(dt.Rows[0]["RequesttypeID"].ToString()) == 3 && VVflag == 0 && dsResult.Tables[1].Rows.Count > 0)
                                msg = msg + " ; " + Save_ePreauthDetails(24, Convert.ToInt64(ClaimID), Convert.ToInt32(Slno));
                            if (dsResult.Tables[0].Rows.Count > 0 && dsResult.Tables[1].Rows.Count > 0)//&& dsResult.Tables[2].Rows.Count > 0)
                            {
                                _objCommon.CommunicationInsert_Common(ref dsResult, Convert.ToInt64(ClaimID), Convert.ToInt32(Slno), Convert.ToInt64(dt.Rows[0]["mainMemberID"].ToString()),
                                    Convert.ToInt64(dt.Rows[0]["policyID"].ToString()), Convert.ToInt64(dt.Rows[0]["providerID"].ToString()), Convert.ToInt32(dt.Rows[0]["BrokerID"].ToString() == "" ? "0" : dt.Rows[0]["BrokerID"].ToString()), Convert.ToInt64(dt.Rows[0]["CorporateID"].ToString()),
                                    Convert.ToInt64(dt.Rows[0]["PayerID"].ToString()), Convert.ToInt32(dt.Rows[0]["IssueID"].ToString()), 24, "MedicalScrutinyController", Convert.ToInt32(Session[SessionValue.UserRegionID]), 0, Convert.ToInt32(dt.Rows[0]["AgentID"].ToString() == "" ? "0" : dt.Rows[0]["AgentID"].ToString()));

                            }
                        }
                    }
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
            return Newtonsoft.Json.JsonConvert.SerializeObject(msg);
        }

        public ActionResult Get_Approval_letter(string ClaimID, string Slno, string StageID)
        {
            try
            {
                DataTable dt = _objMadicalScrutinyVM.Getcommuicationbasic_details(Convert.ToInt64(ClaimID), Convert.ToInt32(Slno));
                DataSet dsResult = _objMadicalScrutinyVM.Get_Final_communication_data(Convert.ToInt64(ClaimID), Convert.ToInt32(Slno));
                if (dsResult.Tables[0].Rows.Count > 0 && dsResult.Tables[1].Rows.Count > 0)//&& dsResult.Tables[2].Rows.Count > 0)
                {
                    string mailcontent = _objCommon.CommunicationInsert_Common(ref dsResult, Convert.ToInt64(ClaimID), Convert.ToInt32(Slno), Convert.ToInt64(dt.Rows[0]["mainMemberID"].ToString()),
                          Convert.ToInt64(dt.Rows[0]["policyID"].ToString()), Convert.ToInt64(dt.Rows[0]["providerID"].ToString()), Convert.ToInt32(dt.Rows[0]["BrokerID"].ToString() == "" ? "0" : dt.Rows[0]["BrokerID"].ToString()), Convert.ToInt64(dt.Rows[0]["CorporateID"].ToString()),
                          Convert.ToInt64(dt.Rows[0]["PayerID"].ToString()), Convert.ToInt32(dt.Rows[0]["IssueID"].ToString()), 24, "MedicalScrutinyController", Convert.ToInt32(Session[SessionValue.UserRegionID]), 0, Convert.ToInt32(dt.Rows[0]["AgentID"].ToString() == "" ? "0" : dt.Rows[0]["AgentID"].ToString()));
                }
                return View();
            }
            catch (Exception ex)
            {
                return RedirectToAction("MCareLogin", "Account");
            }
        }

        //Added for Spectra-iAI integration (SP3V-4924)
        public JsonResult LockiAIProcessClaim(string ClaimID)
        {
            List<object> objRes = new List<object>();
            if (Session[SessionValue.UserRegionID] != null)
            {
                if (cacheobj.IsSet(ClaimID, Convert.ToInt32(Session[SessionValue.LoginUserID])))
                {
                    CacheProvider cache = (CacheProvider)cacheobj.Get(ClaimID);
                    objRes.Add(new { Result = false, ResponseText = "Claim locked by " + cache.UserName + "" });
                }
                else
                {
                    if (cacheobj.IsUserHaveLocks(Convert.ToInt32(Session[SessionValue.LoginUserID]), ClaimID, CacheItemType.ClaimLock))
                    {
                        string msg = "You have already locked Other Claim. Please unlock the Previous Claim to process another claim ";
                        objRes.Add(new { Result = false, ResponseText = msg });
                    }
                    else
                    {
                        CacheProvider cache = new CacheProvider();
                        cache.UserID = Convert.ToInt32(Session[SessionValue.LoginUserID]);
                        //ViewData["LoginUserID"] = Session[SessionValue.LoginUserID];
                        cache.UserName = User.Identity.Name;
                        cache.CachedDatetime = DateTime.Now;
                        cache.CacheitemType = CacheItemType.ClaimLock;
                        new DefaultCacheProvider().Set(ClaimID, cache, int.MaxValue);
                    }
                }
                objRes.Add(new { Result = true, ResponseText = "Success" });
            }
            else
            {
                objRes.Add(new { Result = false, ResponseText = "ErrorCode#1" });
            }
            var jsonResult = Json(JsonConvert.SerializeObject(objRes), JsonRequestBehavior.AllowGet);
            jsonResult.MaxJsonLength = int.MaxValue;
            return jsonResult;
        }

        public JsonResult pushReuploadedDocumentToIAI(string claimData)
        {
            List<object> objRes = new List<object>();
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    JObject objClaim = JObject.Parse(claimData);
                    long claimid = Convert.ToInt64(objClaim["ClaimId"].ToString());
                    int slno = Convert.ToInt16(objClaim["Slno"].ToString());
                    int userRegionId = Convert.ToInt32(Session[SessionValue.UserRegionID]);
                    int statusId; string statusMsg;
                    _objMadicalScrutinyVM.PushClaimBackToiAIDataExtraction(claimid, slno, userRegionId, out statusId, out statusMsg);

                    objRes.Add(new { Result = (statusId == 1) ? true : false, ResponseText = statusMsg });
                }
                else
                {
                    objRes.Add(new { Result = false, ResponseText = "ErrorCode#1" });
                }
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                objRes.Add(new { Result = false, ResponseText = "Error occured while reprocessing" });
            }
            var jsonResult = Json(JsonConvert.SerializeObject(objRes), JsonRequestBehavior.AllowGet);
            jsonResult.MaxJsonLength = int.MaxValue;
            return jsonResult;
        }
        //End (SP3V-4924)

        public string BackToBillingStage(long ClaimID, int SlNo)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.BackToBillingRemarks(ClaimID, SlNo));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "ReferInsDetails_Retrieve", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        public string CheckMultipleStageOpen(long ClaimID, int SlNo)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.CheckMultipleStageOpen(ClaimID, SlNo));
                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                //_objCommon.ErrorLog_Insert(ex.Message, "MedicalScrutinyController", "ReferInsDetails_Retrieve", Session[Resources.SessionValue.LoginUserID].ToString());
                //throw ex;
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        public string GetCoverageEligibility_OPD(long ClaimID, int Slno, int CoverageID)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.GetClaimBPCoverageDetials_OPD(ClaimID, Slno, CoverageID));

                }
                else
                {
                    return "ErrorCode#1";
                }
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }

        }

        public string FetchCashlessSuccessFailedLogs(long ClaimID, int SlNo, string tabid)
        {
            try
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(_objMadicalScrutinyVM.FetchCashlessSuccessFailedLogs(ClaimID, SlNo, tabid));
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
        }

        public class checkcovereligibility
        {
            public List<Claimstatus_msg> status_msg { get; set; }
        }
        public class Claimstatus_msg
        {
            public long RuleID { get; set; }
            public string msg { get; set; }
            public bool result { get; set; }
        }
        public string CheckCoverageEligibility_new(string ClaimID, string Slno, string CoverageID)
        {
            string status_msg = "";
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    DataSet ds = _objMadicalScrutinyVM.GetClaimcoveragedetails(Convert.ToInt64(ClaimID), Convert.ToInt32(Slno), Convert.ToInt32(CoverageID));
                    status_msg = CheckCoverageEligibility_Claim(ds);
                }
                else
                    return "ErrorCode#1";
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
            return status_msg;
        }

        public string CheckCoverageEligibility_Claim(DataSet ds)
        {
            string return_msg = "";
            StringBuilder msg = new StringBuilder();
            bool result = true;
            List<Claimstatus_msg> claimstatus_msg_list = new List<Claimstatus_msg>();
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    if (ds.Tables.Count > 0)
                    {
                        if (ds.Tables[1].Rows.Count > 0)
                        {
                            foreach (DataRow datarow in ds.Tables[1].Rows)
                            {
                                msg.Clear();
                                if (datarow["EffectiveDate"].ToString() != string.Empty)//effective date is not empty
                                {
                                    if (Convert.ToDateTime(datarow["EffectiveDate"].ToString()) > Convert.ToDateTime(ds.Tables[0].Rows[0]["ClaimReceivedDate"].ToString()))
                                    {
                                        msg.Append("EffectiveDate is greater than Claim received Date\n");
                                    }
                                }
                                foreach (DataColumn dc in ds.Tables[1].Columns)
                                {
                                    if (ds.Tables[0].Columns.Contains(dc.ColumnName))
                                    {
                                        if (datarow[dc.ColumnName].ToString() != string.Empty)
                                        {
                                            if (dc.ColumnName == "RelGroupID_P26" || dc.ColumnName == "RelationshipID" || dc.ColumnName == "InsZone" || dc.ColumnName == "TPAProcedureID" || dc.ColumnName == "Grade"
                                           || dc.ColumnName == "Designation" || dc.ColumnName == "RequestTypeID" || dc.ColumnName == "ServiceSubTypeID" || dc.ColumnName == "Accomdation")
                                            {
                                                string[] array = datarow[dc.ColumnName].ToString().Split(',');
                                                if (!Array.Exists(array, element => element == ds.Tables[0].Rows[0][dc.ColumnName].ToString()))
                                                {
                                                    msg.Append(dc.ColumnName + " is not matching with Rule configuration " + dc.ColumnName + "\t\n");
                                                    result = false;
                                                }
                                            }
                                            else if (dc.ColumnName == "Age")
                                            {
                                                int AgeExpression = 0;
                                                if (Convert.ToString(datarow["LimitCatg_P29"]) != string.Empty)
                                                    AgeExpression = Convert.ToInt32(datarow["LimitCatg_P29"]);
                                                int AgeType = 0;
                                                if (Convert.ToString(datarow["AgeTypeID"]) != string.Empty)
                                                    AgeType = Convert.ToInt32(datarow["AgeTypeID"]);

                                                if (Convert.ToString(datarow["AgeTypeID"]) != Convert.ToString(ds.Tables[0].Rows[0]["AgeTypeID"]))
                                                {
                                                    msg.Append("Age Type is not matching with Rule configuration Age Type \t\n");
                                                    result = false;
                                                }

                                                if (AgeExpression == 53)  //==
                                                {
                                                    if (Convert.ToInt32(datarow[dc.ColumnName]) != Convert.ToInt32(ds.Tables[0].Rows[0][dc.ColumnName]))
                                                    {
                                                        msg.Append(dc.ColumnName + " is not matching with Rule configuration " + dc.ColumnName + "\t\n");
                                                        result = false;
                                                    }
                                                }
                                                else if (AgeExpression == 54)//>
                                                {
                                                    if (Convert.ToInt32(datarow[dc.ColumnName]) > Convert.ToInt32(ds.Tables[0].Rows[0][dc.ColumnName]))
                                                    {
                                                        msg.Append(dc.ColumnName + " is not matching with Rule configuration " + dc.ColumnName + "\t\n");
                                                        result = false;
                                                    }
                                                }
                                                else if (AgeExpression == 55)//<
                                                {
                                                    if (Convert.ToInt32(datarow[dc.ColumnName]) < Convert.ToInt32(ds.Tables[0].Rows[0][dc.ColumnName]))
                                                    {
                                                        msg.Append(dc.ColumnName + " is not matching with Rule configuration " + dc.ColumnName + "\t\n");
                                                        result = false;
                                                    }
                                                }
                                                else if (AgeExpression == 56)//>=
                                                {
                                                    if (Convert.ToInt32(datarow[dc.ColumnName]) >= Convert.ToInt32(ds.Tables[0].Rows[0][dc.ColumnName]))
                                                    {
                                                        msg.Append(dc.ColumnName + " is not matching with Rule configuration " + dc.ColumnName + "\t\n");
                                                        result = false;
                                                    }
                                                }
                                                else if (AgeExpression == 57)//<=
                                                {
                                                    if (Convert.ToInt32(datarow[dc.ColumnName]) <= Convert.ToInt32(ds.Tables[0].Rows[0][dc.ColumnName]))
                                                    {
                                                        msg.Append(dc.ColumnName + " is not matching with Rule configuration " + dc.ColumnName + "\t\n");
                                                        result = false;
                                                    }
                                                }

                                            }

                                            else if (Convert.ToString(datarow[dc.ColumnName]) != Convert.ToString(ds.Tables[0].Rows[0][dc.ColumnName]))
                                            {
                                                msg.Append(dc.ColumnName + " is not matching with Rule configuration " + dc.ColumnName + "\t\n");
                                                result = false;
                                            }
                                        }

                                    }

                                    if (dc.ColumnName == "SpecialRuleCondition")
                                    {
                                        int SpecialCondition = 0;
                                        SpecialCondition = Convert.ToInt32(datarow["SpecialRuleCondition"]);
                                        if (SpecialCondition == 399)//When Critical Illness Claims
                                        {
                                            if (Convert.ToBoolean(ds.Tables[0].Rows[0]["isCI"]) == false)
                                            {
                                                msg.Append("This Coverage is applicable for Critical illness claims only. This is Violating with Rule configuration " + dc.ColumnName + "\t\n");
                                                result = false;
                                            }
                                        }
                                        else if (SpecialCondition == 2)//when Claim free
                                        {
                                            if (Convert.ToBoolean(ds.Tables[0].Rows[0]["isClaimFree"]) == false)
                                            {
                                                msg.Append("This Coverage is applicable for Claim free policies only. This is Violating with Rule configuration " + dc.ColumnName + "\t\n");
                                                result = false;
                                            }
                                        }
                                        else if (SpecialCondition == 400)//when PPN Hospital
                                        {
                                            if (Convert.ToBoolean(ds.Tables[0].Rows[0]["IsPPN"]) == false)
                                            {
                                                msg.Append("This Coverage is applicable for PPN Network Hospital only. This is Violating with Rule configuration " + dc.ColumnName + "\t\n");
                                                result = false;
                                            }
                                        }
                                    }
                                    //    if ((dc.ColumnName == "ExternalValueAbs") && (Convert.ToString(datarow["ExternalValueAbs"]) != string.Empty))//Per day limit rules
                                    //    {
                                    //        int Duration = 0;
                                    //        if (Convert.ToString(datarow["Duration"]) != string.Empty)
                                    //            Duration = Convert.ToInt32(datarow["Duration"]);
                                    //        int DeductibleDays = 0;
                                    //        if (Convert.ToString(datarow["IndividualClaimCount"]) != string.Empty)
                                    //            DeductibleDays = Convert.ToInt32(datarow["IndividualClaimCount"]);
                                    //        int DurationType = 0;
                                    //        if (Convert.ToString(datarow["DurationType_P18"]) != string.Empty)
                                    //            DurationType = Convert.ToInt32(datarow["DurationType_P18"]);
                                    //        int LOS = 0;
                                    //        if (Convert.ToString(ds.Tables[0].Rows[0]["LOS"]) != string.Empty)
                                    //            LOS = Convert.ToInt32(ds.Tables[0].Rows[0]["LOS"]);


                                    //        if (DurationType != 61)
                                    //        {
                                    //            msg.Append("Duration Type should be Days in Rule configuration.\t\n");
                                    //            result = false;
                                    //        }
                                    //        if (result)
                                    //            if (LOS < Duration * 24)
                                    //            {
                                    //                msg.Append("LOS should be more than or equal to " + Duration + " days. This is Violating with Rule configuration " + dc.ColumnName + "\t\n");
                                    //                result = false;
                                    //            }
                                    //        //if (result)
                                    //        //{
                                    //        //    if (Math.Round(Convert.ToDouble((LOS / 24) - DeductibleDays)) > 0)
                                    //        //        ClaimAmt = ((LOS / 24) - DeductibleDays) * Convert.ToDouble(datarow[dc.ColumnName]);
                                    //        //}
                                    //    }
                                    //    else if ((dc.ColumnName == "BPComparisionFrom_P52") && (Convert.ToString(datarow["BPComparisionFrom_P52"]) != string.Empty))//Comparision Conditions
                                    //    {

                                    //        int CFrom = 0;
                                    //        if (Convert.ToString(datarow["BPComparisionFrom_P52"]) != string.Empty)
                                    //            CFrom = Convert.ToInt32(datarow["BPComparisionFrom_P52"]);
                                    //        int Expression = 0;
                                    //        if (Convert.ToString(datarow["ExpressionID_P17"]) != string.Empty)
                                    //            Expression = Convert.ToInt32(datarow["ExpressionID_P17"]);

                                    //        int Duration = 0;
                                    //        if (Convert.ToString(datarow["Duration"]) != string.Empty)
                                    //            Duration = Convert.ToInt32(datarow["Duration"]);
                                    //        //int DeductibleDays = 0;
                                    //        //if (Convert.ToString(datarow["IndividualClaimCount"]) != string.Empty)
                                    //        //    DeductibleDays = Convert.ToInt32(datarow["IndividualClaimCount"]);
                                    //        int DurationType = 0;
                                    //        if (Convert.ToString(datarow["DurationType_P18"]) != string.Empty)
                                    //            DurationType = Convert.ToInt32(datarow["DurationType_P18"]);

                                    //        int CTo = 0;
                                    //        if (Convert.ToString(datarow["BPComparisionTo_P52"]) != string.Empty)
                                    //            CTo = Convert.ToInt32(datarow["BPComparisionTo_P52"]);

                                    //        if (CFrom == 208)//208--claim amount 
                                    //        {
                                    //            Double ClaimAmount = 0;
                                    //            if (Convert.ToString(ds.Tables[0].Rows[0]["ClaimAmount"]) != string.Empty)
                                    //                ClaimAmount = Convert.ToDouble(ds.Tables[0].Rows[0]["ClaimAmount"]);

                                    //            if (DurationType != 410)//410--Rupees
                                    //            {
                                    //                msg.Append("Duration/Formula Type should be Rupees while Comparision with Claim Amount in Rule configuration.\t\n");
                                    //                result = false;
                                    //            }
                                    //            if (CTo != 0)//410--Rupees
                                    //            {
                                    //                msg.Append("Comparision TO should not be configured while Comparision with Claim Amount in Rule configuration.\t\n");
                                    //                result = false;
                                    //            }
                                    //            result = ComparisionExpression(Expression, Duration.ToString(), ClaimAmount.ToString(), "ClaimAmount", ref msg);
                                    //            //result = ComparisionExpression(Expression,Duration.ToString(),ClaimAmount.ToString(),dc.ColumnName,ref msg);

                                    //        }
                                    //    }
                                }
                                Claimstatus_msg claimstatus_msg = new Claimstatus_msg();
                                if (msg.ToString() == "")
                                    msg.Append("Success");
                                claimstatus_msg.RuleID = Convert.ToInt64(datarow["RuleID"].ToString());
                                claimstatus_msg.msg = msg.ToString();
                                claimstatus_msg.result = result;
                                claimstatus_msg_list.Add(claimstatus_msg);
                            }
                        }
                        else
                        {
                            Claimstatus_msg claimstatus_msg = new Claimstatus_msg();
                            msg.Append("There is no specific rule created for given coverage");
                            claimstatus_msg.RuleID = 0;
                            claimstatus_msg.msg = msg.ToString();
                            claimstatus_msg.result = false;
                            claimstatus_msg_list.Add(claimstatus_msg);
                        }
                        //checkcovereligibility_list.status_msg = claimstatus_msg_list;
                        return_msg = JsonConvert.SerializeObject(claimstatus_msg_list);
                    }
                }
                else
                    return "ErrorCode#1";
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                return ex.Message;
            }
            return return_msg;
        }


        public JsonResult CRMReviewApproval(long ClaimID, int SlNo, string remarks, string roleID, string MainMemberPolicyID, string PolicyID, string ProviderID,
            string BrokerID, string PayerID, string CorporateID, string InsuranceCompanyID, string AgentID)
        {
            List<object> objRes = new List<object>();
            try
            {
                if (Session[SessionValue.UserRegionID] != null)
                {
                    int userRegionId = Convert.ToInt32(Session[SessionValue.UserRegionID]);
                    int RegionID = Convert.ToInt32(Session[Resources.SessionValue.RegionID]);
                    long statusId; string statusMsg;
                    DataSet dsResult = null;
                    dsResult = _objMadicalScrutinyVM.CRMReviewApproval(ClaimID, SlNo, remarks, userRegionId, RegionID);

                    if (dsResult.Tables[0].Rows.Count > 0 && dsResult.Tables[1].Rows.Count > 0)//&& dsResult.Tables[2].Rows.Count > 0)
                    {
                        _objCommon.CommunicationInsert_Common(ref dsResult, Convert.ToInt64(ClaimID), SlNo, Convert.ToInt64(MainMemberPolicyID),
                            Convert.ToInt64(PolicyID), Convert.ToInt64(ProviderID), Convert.ToInt32(BrokerID == "" ? "0" : BrokerID), Convert.ToInt64(CorporateID),
                            Convert.ToInt64(PayerID), Convert.ToInt32(InsuranceCompanyID), 24, "MedicalScrutinyController", Convert.ToInt32(Session[SessionValue.UserRegionID]), 0, Convert.ToInt32(AgentID == "" ? "0" : AgentID));

                    }

                    objRes.Add(new { Result = true, ResponseText = "success" });
                }
                else
                {
                    objRes.Add(new { Result = false, ResponseText = "ErrorCode#1" });
                }
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName = System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));
                objRes.Add(new { Result = false, ResponseText = "Error occured while reprocessing" });
            }
            var jsonResult = Json(JsonConvert.SerializeObject(objRes), JsonRequestBehavior.AllowGet);
            jsonResult.MaxJsonLength = int.MaxValue;
            return jsonResult;
        }

        public string GetInsurerRejectionMaster(int insurerid, int productid)
        {
            DataSet dataSet = new DataSet();
            dataSet = _objMadicalScrutinyVM.GetInsurerRejectionMaster(insurerid, productid);
            var jsonResult = JsonConvert.SerializeObject(dataSet);
            return jsonResult;
        }
        /// <summary>
        /// Fetches all documents attached to a claim from the DMS and returns each
        /// document's file content as a Base64-encoded string.
        ///
        /// Flow:
        ///   1. POST api/Auth/generatetoken        → short-lived JWT bearer token
        ///   2. GET  api/Document/claimdocumenturls → list of documents with their URLs
        ///   3. For each documentUrl               → download raw bytes → Base64 encode
        ///
        /// Response Data is a list of ClaimDocumentBase64 objects, one per document:
        ///   documentId       — unique document ID from DMS
        ///   documentName     — original file name (e.g. "Discharge Summary.pdf")
        ///   documentCategory — category (e.g. "Medical", "Lab Report")
        ///   documentDate     — date the document was received
        ///   fileType         — extension derived from document name (e.g. "pdf", "jpg")
        ///   base64Content    — the full file encoded as Base64 string
        ///
        /// GET /MedicalScrutiny/GetClaimDocuments?ClaimID=284701&SlNo=1
        /// </summary>
        [HttpGet]
        public async Task<ActionResult> GetClaimDocuments(long ClaimID, int SlNo)
        {
            var res = new ApiResponse<object>();

            try
            {
                if (Session[SessionValue.UserRegionID] == null)
                {
                    res.Success = false;
                    res.ErrorCode = "ErrorCode#1";
                    res.Message = "Session expired. Please log in again.";
                    return Json(res, JsonRequestBehavior.AllowGet);
                }

                string baseUrl = ConfigurationManager.AppSettings["DMSApiURL"].ToString();
                string clientId = ConfigurationManager.AppSettings["ClientID"].ToString();
                string apiKey = ConfigurationManager.AppSettings["DMSAPIKey"].ToString();

                ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

                using (var httpClient = new HttpClient())
                {
                    // ── Step 1: generate bearer token ────────────────────────────────
                    string tokenUrl = baseUrl + "api/Auth/generatetoken";

                    var tokenRequestBody = JsonConvert.SerializeObject(new
                    {
                        clientId = clientId,
                        apiKey = apiKey
                    });

                    var tokenRequest = new HttpRequestMessage(HttpMethod.Post, tokenUrl);
                    tokenRequest.Headers.Add("accept", "*/*");
                    tokenRequest.Content = new StringContent(
                        tokenRequestBody, Encoding.UTF8, "application/json");

                    HttpResponseMessage tokenResponse =
                        await httpClient.SendAsync(tokenRequest, HttpCompletionOption.ResponseHeadersRead);

                    if (!tokenResponse.IsSuccessStatusCode)
                    {
                        res.Success = false;
                        res.Message = "DMS token generation failed. HTTP " +
                                      (int)tokenResponse.StatusCode + " — " +
                                      tokenResponse.ReasonPhrase;
                        return Json(res, JsonRequestBehavior.AllowGet);
                    }

                    string dmsToken = (await tokenResponse.Content.ReadAsStringAsync()).Trim('"');

                    // ── Step 2: get document list for this claim ───────────────────────
                    string docsUrl = baseUrl +
                        "api/Document/claimdocumenturls" +
                        "?claimId=" + ClaimID +
                        "&claimExtNo=" + SlNo;

                    var docsRequest = new HttpRequestMessage(HttpMethod.Get, docsUrl);
                    docsRequest.Headers.Add("accept", "*/*");
                    docsRequest.Headers.Authorization =
                        new AuthenticationHeaderValue("Bearer", dmsToken);

                    HttpResponseMessage docsResponse =
                        await httpClient.SendAsync(docsRequest, HttpCompletionOption.ResponseHeadersRead);

                    string docsRaw = await docsResponse.Content.ReadAsStringAsync();

                    if (!docsResponse.IsSuccessStatusCode)
                    {
                        res.Success = false;
                        res.Message = "DMS document list fetch failed. HTTP " +
                                      (int)docsResponse.StatusCode + " — " + docsRaw;
                        return Json(res, JsonRequestBehavior.AllowGet);
                    }

                    // DMS returns the plain string "No documents found" (not empty JSON) when none exist
                    if (string.IsNullOrWhiteSpace(docsRaw) ||
                        docsRaw.Trim('"') == "No documents found")
                    {
                        res.Success = true;
                        res.Message = "No documents found for this claim.";
                        res.Data = new List<object>();
                        return Json(res, JsonRequestBehavior.AllowGet);
                    }

                    var documents =
                        JsonConvert.DeserializeObject<List<MedicalScrutinyViewModel.DocumentUrlresponse>>(docsRaw);

                    // ── Step 3: download each document and Base64-encode its bytes ────
                    var result = new List<object>();

                    foreach (var doc in documents)
                    {
                        string base64Content = string.Empty;
                        string fileType = string.Empty;
                        string downloadError = string.Empty;

                        try
                        {
                            // Derive file extension from document name
                            if (!string.IsNullOrWhiteSpace(doc.documentName))
                            {
                                string ext = System.IO.Path.GetExtension(doc.documentName);
                                fileType = ext.TrimStart('.').ToLower();
                            }

                            // Many DMS systems return pre-signed S3 URLs that are
                            // self-authenticating via query-string params. Sending an
                            // Authorization header to a pre-signed S3 URL causes HTTP 400
                            // because S3 rejects requests with both query-string auth and
                            // an Authorization header. So we try without auth first.
                            var fileRequest = new HttpRequestMessage(HttpMethod.Get, doc.documentUrl);
                            fileRequest.Headers.Add("accept", "*/*");

                            HttpResponseMessage fileResponse =
                                await httpClient.SendAsync(fileRequest, HttpCompletionOption.ResponseHeadersRead);

                            // If that fails, retry with the Bearer token
                            if (!fileResponse.IsSuccessStatusCode)
                            {
                                var fileRequestWithAuth = new HttpRequestMessage(HttpMethod.Get, doc.documentUrl);
                                fileRequestWithAuth.Headers.Add("accept", "*/*");
                                fileRequestWithAuth.Headers.Authorization =
                                    new AuthenticationHeaderValue("Bearer", dmsToken);

                                fileResponse = await httpClient.SendAsync(
                                    fileRequestWithAuth, HttpCompletionOption.ResponseHeadersRead);
                            }

                            if (fileResponse.IsSuccessStatusCode)
                            {
                                byte[] fileBytes = await fileResponse.Content.ReadAsByteArrayAsync();
                                base64Content = Convert.ToBase64String(fileBytes);
                            }
                            else
                            {
                                string responseBody = string.Empty;
                                try { responseBody = await fileResponse.Content.ReadAsStringAsync(); } catch { }
                                downloadError = "Download failed: HTTP " +
                                                (int)fileResponse.StatusCode + " — " +
                                                fileResponse.ReasonPhrase +
                                                (string.IsNullOrWhiteSpace(responseBody)
                                                    ? string.Empty
                                                    : " | " + responseBody.Substring(0, Math.Min(200, responseBody.Length)));
                            }
                        }
                        catch (Exception docEx)
                        {
                            downloadError = "Error downloading document: " + docEx.Message;
                        }

                        result.Add(new
                        {
                            documentId = doc.documentId,
                            documentName = doc.documentName,
                            documentCategory = doc.documentCategory,
                            documentDate = doc.documentDate,
                            fileType = fileType,
                            base64Content = base64Content,
                            error = downloadError
                        });
                    }

                    res.Success = true;
                    res.Message = result.Count + " document(s) returned.";
                    res.Data = result;

                    return Json(res, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog errorLog = Elmah.ErrorLog.GetDefault(null);
                errorLog.ApplicationName =
                    System.Web.Configuration.WebConfigurationManager.AppSettings["AppName"].ToString();
                errorLog.Log(new Elmah.Error(ex));

                res.Success = false;
                res.Message = "Error while fetching claim documents: " + ex.Message;
                return Json(res, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// <summary>
        /// CHECKPOINT 1: Browser calls this endpoint first.
        /// Flow: Browser → Spectra (this action) → ClaimAI Next.js (/api/audit/start) → Convex
        /// If this fails, check:
        ///   - Is ClaimAIUrl correct in Web.config?
        ///   - Can Spectra server reach ClaimAI server? (network/firewall)
        ///   - Is ClaimAI app running on the target server? (pm2 status)
        /// </summary>
        [HttpPost]
        public ActionResult StartClaimAuditProxy()
        {
            var res = new ApiResponse<object>();
            try
            {
                if (Session[SessionValue.UserRegionID] == null)
                {
                    res.Success = false; res.ErrorCode = "ErrorCode#1";
                    res.Message = "Session expired.";
                    return Json(res);
                }

                // CHECKPOINT 1a: Parse request body from browser
                // If this fails → browser sent malformed JSON
                Request.InputStream.Seek(0, System.IO.SeekOrigin.Begin);
                string body = new System.IO.StreamReader(Request.InputStream).ReadToEnd();
                dynamic payload = Newtonsoft.Json.JsonConvert.DeserializeObject(body);

                string claimId            = payload?.claimId?.ToString() ?? "";
                string hospitalBillBase64 = payload?.hospitalBillBase64?.ToString() ?? "";
                string hospitalFileName   = payload?.hospitalFileName?.ToString() ?? "medical-bill.pdf";
                string tariffBase64       = payload?.tariffBase64?.ToString() ?? "";
                string tariffFileName     = payload?.tariffFileName?.ToString() ?? "";
                string spectraFieldsJson  = payload?.spectraFields != null
                    ? Newtonsoft.Json.JsonConvert.SerializeObject(payload.spectraFields)
                    : "";

                if (string.IsNullOrWhiteSpace(claimId) || string.IsNullOrWhiteSpace(hospitalBillBase64))
                {
                    res.Success = false;
                    res.Message = "CHECKPOINT 1a FAILED: claimId and hospitalBillBase64 are required.";
                    return Json(res);
                }

                // CHECKPOINT 1b: Convert base64 PDFs to bytes
                // If this fails → base64 string is corrupted
                byte[] medBytes;
                try
                {
                    medBytes = Convert.FromBase64String(hospitalBillBase64);
                }
                catch (Exception b64Ex)
                {
                    res.Success = false;
                    res.Message = "CHECKPOINT 1b FAILED: Medical bill base64 decode error: " + b64Ex.Message;
                    return Json(res);
                }

                byte[] tarBytes = null;
                if (!string.IsNullOrWhiteSpace(tariffBase64))
                {
                    try { tarBytes = Convert.FromBase64String(tariffBase64); }
                    catch { /* tariff is optional - skip if decode fails */ }
                }

                // CHECKPOINT 1c: Read ClaimAI URL from Web.config
                // If wrong URL → connection will fail at CHECKPOINT 1d
                string claimAiUrl = (System.Configuration.ConfigurationManager.AppSettings["ClaimAIUrl"] ?? "http://localhost:3000").TrimEnd('/') + "/api/audit/start";

                System.Net.ServicePointManager.SecurityProtocol =
                    System.Net.SecurityProtocolType.Tls12 |
                    System.Net.SecurityProtocolType.Tls11 |
                    System.Net.SecurityProtocolType.Tls;
                System.Net.ServicePointManager.ServerCertificateValidationCallback =
                    (sender, cert, chain, errors) => true;

                // CHECKPOINT 1d: POST to ClaimAI /api/audit/start
                // FAILURE HERE means one of:
                //   a) ClaimAI server is down → check pm2 status on ClaimAI EC2
                //   b) Network blocked → check EC2 security groups / firewall
                //   c) Wrong URL in Web.config ClaimAIUrl key
                //   d) SSL certificate issue → check nginx SSL config on ClaimAI EC2
                // Error message will contain the exact network exception
                // Build multipart body manually — avoids .NET quoting the boundary
                // which breaks Next.js request.formData() parsing
                string boundary = "ClaimAIBoundary" + Guid.NewGuid().ToString("N");
                byte[] multipartBody = BuildMultipartBody(
                    boundary, claimId, hospitalFileName, medBytes,
                    tariffFileName, tarBytes, spectraFieldsJson);

                using (var client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(3);

                    var request = new System.Net.Http.HttpRequestMessage(
                        System.Net.Http.HttpMethod.Post, claimAiUrl);
                    var bodyContent = new System.Net.Http.ByteArrayContent(multipartBody);
                    bodyContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(
                        "multipart/form-data; boundary=" + boundary);
                    request.Content = bodyContent;

                    System.Net.Http.HttpResponseMessage response;
                    string responseBody;
                    try
                    {
                        // CHECKPOINT 1d: Actual HTTP call to ClaimAI server
                        response     = client.SendAsync(request).GetAwaiter().GetResult();
                        responseBody = response.Content.ReadAsStringAsync().Result;
                    }
                    catch (Exception httpEx)
                    {
                        // NETWORK ERROR — request never reached ClaimAI
                        // Common causes: DNS resolution failed, connection refused, SSL error, timeout
                        res.Success = false;
                        res.Message = "CHECKPOINT 1d FAILED: Cannot reach ClaimAI at [" + claimAiUrl + "]. "
                            + "Network error: " + httpEx.Message
                            + (httpEx.InnerException != null ? " | Inner: " + httpEx.InnerException.Message : "");
                        Elmah.ErrorLog.GetDefault(null).Log(new Elmah.Error(httpEx));
                        return Json(res);
                    }

                    // CHECKPOINT 1e: ClaimAI returned non-200 response
                    // FAILURE HERE means request reached ClaimAI but ClaimAI rejected it
                    // Check ClaimAI logs / Next.js console for errors
                    if (!response.IsSuccessStatusCode)
                    {
                        res.Success = false;
                        res.Message = "CHECKPOINT 1e FAILED: ClaimAI returned HTTP " + (int)response.StatusCode
                            + " from [" + claimAiUrl + "]. Response: " + responseBody;
                        return Json(res);
                    }

                    // CHECKPOINT 1f: Parse jobId from ClaimAI response
                    // FAILURE HERE means ClaimAI reached Convex but Convex rejected or returned unexpected response
                    // Check NEXT_PUBLIC_CONVEX_URL env var in ClaimAI .env.local
                    dynamic convexRes = Newtonsoft.Json.JsonConvert.DeserializeObject(responseBody);
                    string jobId = convexRes?.jobId?.ToString() ?? "";
                    bool convexSuccess = convexRes?.success == null || convexRes.success == true;

                    if (!convexSuccess || string.IsNullOrWhiteSpace(jobId))
                    {
                        string errMsg = convexRes?.error?.ToString() ?? convexRes?.message?.ToString() ?? responseBody;
                        res.Success = false;
                        res.Message = "CHECKPOINT 1f FAILED: ClaimAI reached but no jobId returned. "
                            + "Likely Convex connection issue. Check NEXT_PUBLIC_CONVEX_URL in ClaimAI .env.local. "
                            + "ClaimAI error: " + errMsg;
                        return Json(res);
                    }

                    // CHECKPOINT 1 SUCCESS: jobId received, iframe will load
                    res.Success = true;
                    res.Message = "Job started successfully. ClaimAI URL: " + claimAiUrl;
                    res.Data    = new { jobId };
                    return Json(res);
                }
            }
            catch (Exception ex)
            {
                // UNEXPECTED ERROR — check Elmah logs for full stack trace
                Elmah.ErrorLog.GetDefault(null).Log(new Elmah.Error(ex));
                res.Success = false;
                res.Message = "StartClaimAuditProxy unexpected error: " + ex.Message
                    + (ex.InnerException != null ? " | Inner: " + ex.InnerException.Message : "");
                return Json(res);
            }
        }

        /// <summary>
        /// Builds a raw multipart/form-data body without quoting the boundary.
        /// .NET MultipartFormDataContent quotes the boundary string which breaks
        /// Next.js request.formData() parser — so we build it manually.
        /// </summary>
        private byte[] BuildMultipartBody(
            string boundary,
            string claimId,
            string medFileName, byte[] medBytes,
            string tarFileName, byte[] tarBytes,
            string spectraFieldsJson)
        {
            var enc = System.Text.Encoding.UTF8;
            using (var ms = new System.IO.MemoryStream())
            {
                WriteMultipartLine(ms, enc, "--" + boundary);
                WriteMultipartLine(ms, enc, "Content-Disposition: form-data; name=\"claimId\"");
                WriteMultipartLine(ms, enc, "");
                WriteMultipartLine(ms, enc, claimId);

                WriteMultipartLine(ms, enc, "--" + boundary);
                WriteMultipartLine(ms, enc, "Content-Disposition: form-data; name=\"medicalBill\"; filename=\"" + medFileName + "\"");
                WriteMultipartLine(ms, enc, "Content-Type: application/pdf");
                WriteMultipartLine(ms, enc, "");
                WriteMultipartBytes(ms, enc, medBytes);

                if (tarBytes != null && !string.IsNullOrWhiteSpace(tarFileName))
                {
                    WriteMultipartLine(ms, enc, "--" + boundary);
                    WriteMultipartLine(ms, enc, "Content-Disposition: form-data; name=\"tariffBill\"; filename=\"" + tarFileName + "\"");
                    WriteMultipartLine(ms, enc, "Content-Type: application/pdf");
                    WriteMultipartLine(ms, enc, "");
                    WriteMultipartBytes(ms, enc, tarBytes);
                }

                if (!string.IsNullOrWhiteSpace(spectraFieldsJson))
                {
                    WriteMultipartLine(ms, enc, "--" + boundary);
                    WriteMultipartLine(ms, enc, "Content-Disposition: form-data; name=\"spectraFields\"");
                    WriteMultipartLine(ms, enc, "");
                    WriteMultipartLine(ms, enc, spectraFieldsJson);
                }

                var closing = enc.GetBytes("--" + boundary + "--\r\n");
                ms.Write(closing, 0, closing.Length);
                return ms.ToArray();
            }
        }

        private void WriteMultipartLine(System.IO.MemoryStream ms, System.Text.Encoding enc, string text)
        {
            var b = enc.GetBytes(text + "\r\n");
            ms.Write(b, 0, b.Length);
        }

        private void WriteMultipartBytes(System.IO.MemoryStream ms, System.Text.Encoding enc, byte[] data)
        {
            ms.Write(data, 0, data.Length);
            var crlf = enc.GetBytes("\r\n");
            ms.Write(crlf, 0, crlf.Length);
        }

        #region ClaimAI Document Helpers

        /// <summary>
        /// PSU insurer IDs: 5=UIIC, 6=NIC, 7=OIC, 8=NIAC
        /// </summary>
        private static readonly System.Collections.Generic.HashSet<int> PsuInsurerIds =
            new System.Collections.Generic.HashSet<int> { 5, 6, 7, 8 };

        /// <summary>
        /// STAGING STEP 1 — Disease classifier.
        ///
        /// Determines whether a claim is a disease type that ClaimAI supports
        /// for pre-processing (currently cataract and maternity). The staging
        /// worker uses this to decide: supported -> process via ClaimAI; else
        /// -> move straight to "Ready for Adjudication" (StageID 5).
        ///
        /// Classification is by the claim's Level-3 procedure (level3_ID), which
        /// joins to TPAProcedures and exposes Level1 (the medical specialty):
        ///   - Ophtholmology                         -> "cataract"
        ///   - Obstetrics and Gynecology / OBG       -> "maternity"
        ///   - anything else                         -> "other"
        ///
        /// To support a new disease later, add its Level1 specialty value(s) to
        /// the relevant set below — no other code change needed.
        ///
        /// Returns "cataract", "maternity", or "other". On any error or missing
        /// data, returns "other" (safe default — claim goes straight to stage 5
        /// rather than being held).
        /// </summary>
        private string GetClaimDiseaseTypeForStaging(long claimId, string connStr)
        {
            // Specialties (TPAProcedures.Level1) that map to a supported disease.
            // Extend these sets to add diseases later.
            var cataractSpecialties = new System.Collections.Generic.HashSet<string>(
                StringComparer.OrdinalIgnoreCase) { "Ophtholmology" };
            var maternitySpecialties = new System.Collections.Generic.HashSet<string>(
                StringComparer.OrdinalIgnoreCase) { "Obstetrics and Gynecology", "OBG" };

            try
            {
                if (claimId <= 0 || string.IsNullOrWhiteSpace(connStr))
                    return "other";

                // 1. Latest Slno for the claim.
                int slNoInt = 1;
                using (var conn = new System.Data.SqlClient.SqlConnection(connStr))
                {
                    conn.Open();
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT TOP 1 Slno FROM Claimsdetails WHERE ClaimID=@cid AND ISNULL(Deleted,0)=0 ORDER BY Slno";
                    cmd.Parameters.AddWithValue("@cid", claimId);
                    var val = cmd.ExecuteScalar();
                    if (val != null && val != DBNull.Value) slNoInt = Convert.ToInt32(val);
                }

                // 2. level3_ID from the medical scrutiny row.
                int level3Id = 0;
                using (var conn = new System.Data.SqlClient.SqlConnection(connStr))
                {
                    conn.Open();
                    var cmd = conn.CreateCommand();
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.CommandText = "USP_ClaimMedicalScrutiny_Retrieve";
                    cmd.Parameters.AddWithValue("@ClaimID", claimId);
                    cmd.Parameters.AddWithValue("@Slno",    slNoInt);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (rdr.Read())
                        {
                            int ord = -1;
                            try { ord = rdr.GetOrdinal("level3_ID"); } catch { ord = -1; }
                            if (ord >= 0 && !rdr.IsDBNull(ord))
                                level3Id = Convert.ToInt32(rdr[ord]);
                        }
                    }
                }

                if (level3Id <= 0) return "other";

                // 3. Level1 (specialty) for that level3 procedure.
                string level1 = "";
                using (var conn = new System.Data.SqlClient.SqlConnection(connStr))
                {
                    conn.Open();
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT TOP 1 Level1 FROM TPAProcedures WHERE ID=@id AND ISNULL(Deleted,0)=0";
                    cmd.Parameters.AddWithValue("@id", level3Id);
                    var val = cmd.ExecuteScalar();
                    if (val != null && val != DBNull.Value) level1 = val.ToString().Trim();
                }

                if (string.IsNullOrWhiteSpace(level1)) return "other";

                if (cataractSpecialties.Contains(level1))  return "cataract";
                if (maternitySpecialties.Contains(level1)) return "maternity";
                return "other";
            }
            catch (Exception ex)
            {
                TariffLog("[Staging] GetClaimDiseaseTypeForStaging failed for claimId=" + claimId + ": " + ex.Message);
                return "other"; // safe default — claim goes straight to stage 5
            }
        }

        /// <summary>
        /// Gets insurer ID and shortname for a claim from DB.
        /// </summary>
        private void GetClaimInsurerInfo(long claimId, string connStr,
            out int insurerIdOut, out string insurerCodeOut)
        {
            insurerIdOut   = 0;
            insurerCodeOut = "";
            try
            {
                using (var conn = new System.Data.SqlClient.SqlConnection(connStr))
                {
                    conn.Open();
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        SELECT ia.ID, ia.Code, ia.ShortName
                        FROM Claims c WITH(NOLOCK)
                        JOIN Mst_IssuingAuthority ia WITH(NOLOCK) ON ia.ID = c.IssueID
                        WHERE c.ID = @cid AND ISNULL(c.Deleted,0)=0";
                    cmd.Parameters.AddWithValue("@cid", claimId);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (rdr.Read())
                        {
                            insurerIdOut   = rdr["ID"] != DBNull.Value ? Convert.ToInt32(rdr["ID"]) : 0;
                            insurerCodeOut = (rdr["Code"] != DBNull.Value ? rdr["Code"].ToString().Trim() : "")
                                + " " +
                                (rdr["ShortName"] != DBNull.Value ? rdr["ShortName"].ToString().Trim() : "");
                        }
                    }
                }
            }
            catch { /* return defaults */ }
        }

        /// <summary>
        /// Recursively walks a zip stream and collects every leaf tariff file
        /// (.pdf/.xlsx/.xls) as (displayName, lastModified, bytes).
        ///
        /// The displayName carries the FULL CHAIN of containing inner-zip names
        /// (with the ".zip" extension stripped), joined by "_", so the AI can
        /// see which zip a file came from. The OUTERMOST zip (the root
        /// "tariff.zip") is NOT part of the prefix — only inner zips are.
        ///
        /// Examples (root = tariff.zip):
        ///   tariff.zip/file.pdf                  -> "file.pdf"
        ///   tariff.zip/gipsa.zip/file.pdf        -> "gipsa_file.pdf"
        ///   tariff.zip/gipsa.zip/2024.zip/f.pdf  -> "gipsa_2024_f.pdf"
        ///
        /// prefix: cumulative prefix for the current zip level ("" at root).
        /// </summary>
        private void CollectTariffFilesFromZip(
            System.IO.Stream zipStream,
            string prefix,
            System.Collections.Generic.List<System.Tuple<string, DateTime, byte[]>> sink)
        {
            using (var zip = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Read, true))
            {
                foreach (var entry in zip.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue; // skip folder entries
                    var nameLow = entry.Name.ToLower();

                    if (nameLow.EndsWith(".zip"))
                    {
                        // Nested zip: extend the prefix with this zip's name
                        // (without ".zip") and recurse.
                        string innerBaseName = entry.Name.Substring(0, entry.Name.Length - 4); // strip ".zip"
                        string childPrefix = string.IsNullOrEmpty(prefix)
                            ? innerBaseName
                            : prefix + "_" + innerBaseName;
                        try
                        {
                            using (var entryStream = entry.Open())
                            using (var ms = new System.IO.MemoryStream())
                            {
                                entryStream.CopyTo(ms);
                                ms.Position = 0;
                                CollectTariffFilesFromZip(ms, childPrefix, sink);
                            }
                        }
                        catch (Exception zEx)
                        {
                            TariffLog("[Tariff] Failed to open nested zip " + entry.Name + ": " + zEx.Message);
                        }
                    }
                    else if (nameLow.EndsWith(".pdf") ||
                             nameLow.EndsWith(".xlsx") ||
                             nameLow.EndsWith(".xls"))
                    {
                        string displayName = string.IsNullOrEmpty(prefix)
                            ? entry.Name
                            : prefix + "_" + entry.Name;
                        using (var stream = entry.Open())
                        using (var ms = new System.IO.MemoryStream())
                        {
                            stream.CopyTo(ms);
                            sink.Add(System.Tuple.Create(displayName, entry.LastWriteTime.DateTime, ms.ToArray()));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Picks the best tariff file from a list of (filename, lastModified, bytes) entries
        /// using PSU/Private priority rules with extended fallbacks.
        /// Priority:
        ///   PSU:     1) Insurer code/name  2) GIPSA (not SOC)  3) GIPSA SOC  4) All/Pvt Insurers
        ///   Private: 1) Insurer code/name  2) All/Pvt Insurers
        ///   Both:    5) FHPL Rate List  6) Filename contains Tariff  7) Any file
        /// Returns null if no candidates.
        /// </summary>
        private System.Tuple<string, byte[]> PickBestTariffFile(
            System.Collections.Generic.List<System.Tuple<string, DateTime, byte[]>> candidates,
            bool isPsu, string insurerCode)
        {
            if (candidates == null || candidates.Count == 0) return null;

            var code = (insurerCode ?? "").ToLower();

            // Build priority tiers
            var tiers = new System.Collections.Generic.List<
                System.Collections.Generic.List<System.Tuple<string, DateTime, byte[]>>>();

            if (isPsu)
            {
                // P1: insurer-specific
                tiers.Add(candidates.FindAll(f => !string.IsNullOrEmpty(code) &&
                    code.Split(new[]{' '}, StringSplitOptions.RemoveEmptyEntries)
                        .Any(c2 => c2.Length > 1 && f.Item1.ToLower().Contains(c2))));
                // P2: GIPSA (not SOC)
                tiers.Add(candidates.FindAll(f => f.Item1.ToLower().Contains("gipsa") &&
                    !f.Item1.ToLower().Contains("soc")));
                // P3: GIPSA SOC
                tiers.Add(candidates.FindAll(f => f.Item1.ToLower().Contains("gipsa") &&
                    f.Item1.ToLower().Contains("soc")));
                // P4: All Insurers / Pvt / Private
                tiers.Add(candidates.FindAll(f => f.Item1.ToLower().Contains("all insurer") ||
                    f.Item1.ToLower().Contains("pvt insurer") ||
                    f.Item1.ToLower().Contains("private insurer")));
            }
            else
            {
                // P1: insurer-specific
                tiers.Add(candidates.FindAll(f => !string.IsNullOrEmpty(code) &&
                    code.Split(new[]{' '}, StringSplitOptions.RemoveEmptyEntries)
                        .Any(c2 => c2.Length > 1 && f.Item1.ToLower().Contains(c2))));
                // P2: All Insurers / Pvt / Private
                tiers.Add(candidates.FindAll(f => f.Item1.ToLower().Contains("all insurer") ||
                    f.Item1.ToLower().Contains("pvt insurer") ||
                    f.Item1.ToLower().Contains("private insurer")));
            }

            // P_n: FHPL Rate List (fallback for all insurer types)
            tiers.Add(candidates.FindAll(f =>
            {
                var n = f.Item1.ToLower();
                return n.Contains("fhpl") || n.Contains("rate list") || n.Contains("ratelist");
            }));

            // P_n+1: filename contains "tariff"
            tiers.Add(candidates.FindAll(f => f.Item1.ToLower().Contains("tariff")));

            // P_last: any file
            tiers.Add(new System.Collections.Generic.List<System.Tuple<string, DateTime, byte[]>>(candidates));

            // Pick from first non-empty tier → latest modified → skip unreadable files
            foreach (var tier in tiers)
            {
                if (tier == null || tier.Count == 0) continue;
                tier.Sort((a, b) => b.Item2.CompareTo(a.Item2)); // latest first
                foreach (var candidate in tier)
                {
                    byte[] converted = EnsurePdf(candidate.Item1, candidate.Item3);
                    if (converted != null) return System.Tuple.Create(candidate.Item1, converted);
                }
            }

            return null;
        }




        /// <summary>
        /// GET /MedicalScrutiny/GetClaimType?claimId=X
        /// Returns the claim type (cataract/maternity/other) based on ICD code from claimsdetails.
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        [OverrideAuthorization]
        public ActionResult GetClaimType(string claimId)
        {
            try
            {
                long claimIdLong;
                if (!long.TryParse((claimId ?? "").Trim(), out claimIdLong) || claimIdLong <= 0)
                    return Json(new { success = false, error = "Invalid claimId" }, JsonRequestBehavior.AllowGet);

                string connStr = System.Configuration.ConfigurationManager
                                       .ConnectionStrings["McarePlusEntities"]?.ConnectionString ?? "";
                if (connStr.StartsWith("metadata=", StringComparison.OrdinalIgnoreCase))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(
                        connStr, @"provider connection string=""([^""]+)""",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (m.Success) connStr = m.Groups[1].Value.Replace("&quot;", "\"");
                }

                using (var conn = new System.Data.SqlClient.SqlConnection(connStr))
                {
                    conn.Open();

                    // Get claimdiagnosis (PropertyID=87 value) and ICD code from claimsdetails
                    // Read Diagnosis text column directly from ClaimsDetails
                    string diagnosisText = "";
                    using (var diagCmd = new System.Data.SqlClient.SqlCommand(
                        "SELECT TOP 1 Diagnosis FROM Claimsdetails WITH(NOLOCK) WHERE ClaimID=@ClaimID AND ISNULL(Deleted,0)=0 ORDER BY SlNo DESC", conn))
                    {
                        diagCmd.Parameters.AddWithValue("@ClaimID", claimIdLong);
                        var diagVal = diagCmd.ExecuteScalar();
                        diagnosisText = diagVal != null && diagVal != DBNull.Value ? diagVal.ToString().Trim() : "";
                    }

                    // Log diagnosis text
                    try {
                        string logDir = System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data/Logs");
                        if (!System.IO.Directory.Exists(logDir)) System.IO.Directory.CreateDirectory(logDir);
                        System.IO.File.AppendAllText(System.IO.Path.Combine(logDir, "ClaimType_" + DateTime.Now.ToString("yyyyMMdd") + ".log"),
                            DateTime.Now.ToString("HH:mm:ss") + $" claimId={claimIdLong} diagnosis={diagnosisText}\n");
                    } catch { }

                    if (string.IsNullOrWhiteSpace(diagnosisText))
                        return Json(new { success = true, claimType = "other", diagnosis = diagnosisText }, JsonRequestBehavior.AllowGet);

                    // Call ClaimAI to classify the diagnosis text
                    string claimType = "other";
                    string claimAiUrl = (System.Configuration.ConfigurationManager.AppSettings["ClaimAIUrl"] ?? "").TrimEnd('/');
                    string aiCallError = "";
                    string aiResponseBody = "";

                    if (string.IsNullOrEmpty(claimAiUrl))
                    {
                        aiCallError = "ClaimAIUrl not configured in Web.config";
                    }
                    else
                    {
                        try
                        {
                            // Force TLS 1.2 + bypass SSL cert validation — match what audit/start uses
                            // audit/start works fine with these settings; mirror them here.
                            System.Net.ServicePointManager.SecurityProtocol =
                                System.Net.SecurityProtocolType.Tls12 |
                                System.Net.SecurityProtocolType.Tls11 |
                                System.Net.SecurityProtocolType.Tls;
                            System.Net.ServicePointManager.ServerCertificateValidationCallback =
                                (sender, cert, chain, errors) => true;

                            using (var http = new System.Net.Http.HttpClient())
                            {
                                http.Timeout = TimeSpan.FromSeconds(15);
                                var payload  = Newtonsoft.Json.JsonConvert.SerializeObject(new { diagnosis = diagnosisText });
                                var body     = new System.Net.Http.StringContent(payload, System.Text.Encoding.UTF8, "application/json");
                                var fullUrl  = claimAiUrl + "/api/classify-claim-type";
                                var res      = http.PostAsync(fullUrl, body).GetAwaiter().GetResult();
                                aiResponseBody = res.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                                if (res.IsSuccessStatusCode)
                                {
                                    dynamic result = Newtonsoft.Json.JsonConvert.DeserializeObject(aiResponseBody);
                                    claimType = result?.claimType?.ToString() ?? "other";
                                }
                                else
                                {
                                    aiCallError = "HTTP " + (int)res.StatusCode + " from ClaimAI";
                                }
                            }
                        }
                        catch (Exception aiEx)
                        {
                            aiCallError = aiEx.GetType().Name + ": " + aiEx.Message;
                            if (aiEx.InnerException != null) aiCallError += " | inner: " + aiEx.InnerException.Message;
                        }
                    }

                    // Always log detailed result so we can debug from log file
                    try {
                        string logDir = System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data/Logs");
                        if (!System.IO.Directory.Exists(logDir)) System.IO.Directory.CreateDirectory(logDir);
                        System.IO.File.AppendAllText(System.IO.Path.Combine(logDir, "ClaimType_" + DateTime.Now.ToString("yyyyMMdd") + ".log"),
                            DateTime.Now.ToString("HH:mm:ss") + $" claimId={claimIdLong} url={claimAiUrl} result={claimType} error={aiCallError} responseBody={aiResponseBody}\n");
                    } catch { }

                    return Json(new {
                        success   = true,
                        claimType = claimType,
                        diagnosis = diagnosisText
                    }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// GET /MedicalScrutiny/GetDocumentsForStaging
        /// Returns medical bill + tariff as base64 for ClaimAI staging pre-processing.
        /// Called by ClaimAI staging webhook during background processing.
        /// </summary>
        [HttpGet]
        public ActionResult GetDocumentsForStaging(string claimId, string slNo = "1")
        {
            try
            {
                string stagingKey = System.Configuration.ConfigurationManager.AppSettings["StagingApiKey"] ?? "";
                string reqKey     = Request.Headers["x-staging-key"] ?? "";
                if (!string.IsNullOrEmpty(stagingKey) && reqKey != stagingKey)
                    return Json(new { Success = false, Message = "Unauthorized" }, JsonRequestBehavior.AllowGet);

                long cId; int sNo = 1;
                if (!long.TryParse((claimId ?? "").Trim(), out cId) || cId <= 0)
                    return Json(new { Success = false, Message = "Invalid claimId" }, JsonRequestBehavior.AllowGet);
                int.TryParse(slNo ?? "1", out sNo);

                // ── Medical Bill ─────────────────────────────────────────────────────
                string billBase64 = null;
                string billFileName = null;
                try
                {
                    string baseUrl = Request.Url.Scheme + "://" + Request.Url.Authority;
                    var http = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(
                        baseUrl + "/MedicalScrutiny/GetMedicalBillDocument?claimId=" + cId + "&slNo=" + sNo);
                    http.Method = "GET";
                    http.CookieContainer = new System.Net.CookieContainer();
                    foreach (string key in Request.Cookies.AllKeys)
                        http.CookieContainer.Add(new System.Net.Cookie(key, Request.Cookies[key].Value, "/", Request.Url.Host));

                    using (var resp = (System.Net.HttpWebResponse)http.GetResponse())
                    using (var sr = new System.IO.StreamReader(resp.GetResponseStream()))
                    {
                        dynamic billObj = Newtonsoft.Json.JsonConvert.DeserializeObject(sr.ReadToEnd());
                        if (billObj != null && billObj.Success == true && billObj.Data != null)
                        {
                            billBase64   = (string)billObj.Data.base64Content;
                            billFileName = (string)billObj.Data.fileName ?? (cId + "-bill.pdf");
                        }
                    }
                }
                catch (Exception billEx)
                {
                    System.Diagnostics.Debug.WriteLine("[Staging] Bill fetch error: " + billEx.Message);
                }

                if (string.IsNullOrEmpty(billBase64))
                    return Json(new { Success = false, Message = "No medical bill found for claimId=" + claimId }, JsonRequestBehavior.AllowGet);

                // ── Tariff (optional) ────────────────────────────────────────────────
                string tariffBase64 = null;
                string tariffFileName = null;
                try
                {
                    string baseUrl2 = Request.Url.Scheme + "://" + Request.Url.Authority;
                    var http2 = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(
                        baseUrl2 + "/MedicalScrutiny/GetTariffDocument?claimId=" + cId + "&slNo=" + sNo);
                    http2.Method = "GET";
                    http2.CookieContainer = new System.Net.CookieContainer();
                    foreach (string key in Request.Cookies.AllKeys)
                        http2.CookieContainer.Add(new System.Net.Cookie(key, Request.Cookies[key].Value, "/", Request.Url.Host));

                    using (var resp2 = (System.Net.HttpWebResponse)http2.GetResponse())
                    using (var sr2 = new System.IO.StreamReader(resp2.GetResponseStream()))
                    {
                        dynamic tarObj = Newtonsoft.Json.JsonConvert.DeserializeObject(sr2.ReadToEnd());
                        if (tarObj != null && tarObj.Success == true && tarObj.Data != null)
                        {
                            tariffBase64   = (string)tarObj.Data.base64Content;
                            tariffFileName = (string)tarObj.Data.fileName ?? (cId + "-tariff.pdf");
                        }
                    }
                }
                catch (Exception tarEx)
                {
                    System.Diagnostics.Debug.WriteLine("[Staging] Tariff fetch error (optional): " + tarEx.Message);
                }

                var js = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                return Content(js.Serialize(new
                {
                    Success        = true,
                    BillBase64     = billBase64,
                    BillFileName   = billFileName,
                    TariffBase64   = tariffBase64,
                    TariffFileName = tariffFileName,
                    ClaimId        = claimId,
                    SlNo           = sNo
                }), "application/json");
            }
            catch (Exception ex)
            {
                return Json(new { Success = false, Message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // STAGING — STEP 4: the batch worker.
        //
        // GET /MedicalScrutiny/ProcessStagingClaims
        //
        // Triggered every 5 min by Windows Task Scheduler (Modified Option A).
        // For each claim held at StageID 52 that has not yet been processed:
        //   1. classify disease (GetClaimDiseaseTypeForStaging)
        //   2. if not cataract/maternity -> mark 'skipped', flip to stage 5
        //   3. else: fetch docs (env-based, reuses GetMedicalBillDocument /
        //      GetTariffDocument internally), submit to ClaimAI /api/audit/start
        //      (which processes synchronously and returns a jobId), then pull the
        //      result from /api/staging/result, store it in ClaimAI_Results, and
        //      flip the claim to stage 5.
        //   4. on ANY failure -> mark 'failed', flip to stage 5 (fail-open: the
        //      doctor processes on-demand when they open the claim, as today).
        //
        // AllowAnonymous/OverrideAuthorization: the scheduler calls this with no
        // user session. Optional shared-secret check via StagingApiKey header.
        // ════════════════════════════════════════════════════════════════════
        [HttpGet]
        [AllowAnonymous]
        [OverrideAuthorization]
        public ActionResult ProcessStagingClaims(int batchSize = 10)
        {
            int processed = 0, skipped = 0, failed = 0;
            var details = new System.Collections.Generic.List<object>();

            try
            {
                // Optional auth: if StagingApiKey is configured, require the header.
                string stagingKey = System.Configuration.ConfigurationManager.AppSettings["StagingApiKey"] ?? "";
                if (!string.IsNullOrEmpty(stagingKey))
                {
                    string reqKey = Request.Headers["x-staging-key"] ?? Request.QueryString["key"] ?? "";
                    if (reqKey != stagingKey)
                        return Json(new { Success = false, Message = "Unauthorized" }, JsonRequestBehavior.AllowGet);
                }

                string connStr = GetStagingConnString();
                if (string.IsNullOrWhiteSpace(connStr))
                    return Json(new { Success = false, Message = "No connection string." }, JsonRequestBehavior.AllowGet);

                if (batchSize <= 0 || batchSize > 50) batchSize = 10;

                // ── Find claims to process: StageID 52, no ClaimAI_Results row yet
                //    (or status NULL). 'processing'/'done'/'failed'/'skipped' are
                //    excluded so we never double-pick. ───────────────────────────
                var claimIds = new System.Collections.Generic.List<long>();
                using (var conn = new System.Data.SqlClient.SqlConnection(connStr))
                {
                    conn.Open();
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        SELECT TOP (@bs) c.ID
                        FROM Claims c WITH (NOLOCK)
                        LEFT JOIN dbo.ClaimAI_Results r WITH (NOLOCK) ON r.ClaimID = c.ID
                        WHERE c.StageID = 52
                          AND ISNULL(c.Deleted, 0) = 0
                          AND (r.ID IS NULL OR r.ProcessingStatus IS NULL)
                        ORDER BY c.ID DESC";
                    cmd.Parameters.AddWithValue("@bs", batchSize);
                    using (var rdr = cmd.ExecuteReader())
                        while (rdr.Read()) claimIds.Add(Convert.ToInt64(rdr["ID"]));
                }

                foreach (long claimId in claimIds)
                {
                    try
                    {
                        // Latest Slno for this claim.
                        int slNo = 1;
                        using (var conn = new System.Data.SqlClient.SqlConnection(connStr))
                        {
                            conn.Open();
                            var cmd = conn.CreateCommand();
                            cmd.CommandText = "SELECT TOP 1 Slno FROM Claimsdetails WHERE ClaimID=@cid AND ISNULL(Deleted,0)=0 ORDER BY Slno";
                            cmd.Parameters.AddWithValue("@cid", claimId);
                            var v = cmd.ExecuteScalar();
                            if (v != null && v != DBNull.Value) slNo = Convert.ToInt32(v);
                        }

                        // 1. Classify disease.
                        string disease = GetClaimDiseaseTypeForStaging(claimId, connStr);

                        // 2. Not supported -> skip, go straight to stage 5.
                        if (disease != "cataract" && disease != "maternity")
                        {
                            UpsertStagingResult(connStr, claimId, slNo, disease, "skipped",
                                null, null, null, null, null);
                            SetClaimStage(connStr, claimId, 5);
                            skipped++;
                            details.Add(new { claimId, disease, status = "skipped" });
                            continue;
                        }

                        // 3. Lock: mark 'processing' so a concurrent run won't re-pick.
                        UpsertStagingResult(connStr, claimId, slNo, disease, "processing",
                            null, null, null, null, null);

                        // 4. Fetch docs internally (no session) — reuse existing
                        //    env-based GetMedicalBillDocument / GetTariffDocument.
                        string billB64 = null, billName = null, tarB64 = null, tarName = null;
                        _stagingInternalCall = true;
                        try
                        {
                            ExtractDocBase64(GetMedicalBillDocument(claimId.ToString(), slNo.ToString()),
                                out billB64, out billName);
                            try
                            {
                                ExtractDocBase64(GetTariffDocument(claimId.ToString(), slNo.ToString()),
                                    out tarB64, out tarName);
                            }
                            catch { /* tariff optional */ }
                        }
                        finally
                        {
                            _stagingInternalCall = false;
                        }

                        if (string.IsNullOrEmpty(billB64))
                            throw new Exception("Medical bill not found for claimId=" + claimId);

                        // 5. Submit to ClaimAI (synchronous: returns jobId once done).
                        string jobId = SubmitClaimToClaimAI(claimId.ToString(), billName, billB64,
                            tarName, tarB64, disease);
                        if (string.IsNullOrWhiteSpace(jobId))
                            throw new Exception("ClaimAI did not return a jobId.");

                        // 6. Pull the stored result.
                        string analysisJson, benefitPlan, pdfUrl, aiStatus;
                        GetStagingResultFromClaimAI(jobId, out aiStatus, out analysisJson,
                            out benefitPlan, out pdfUrl);

                        if (aiStatus == "error")
                            throw new Exception("ClaimAI reported processing error for jobId=" + jobId);

                        // 7. Fetch processed PDF bytes from the URL (best-effort).
                        byte[] pdfBytes = null;
                        if (!string.IsNullOrWhiteSpace(pdfUrl))
                        {
                            try { pdfBytes = DownloadBytes(pdfUrl); }
                            catch (Exception pdfEx)
                            { System.Diagnostics.Debug.WriteLine("[Staging] PDF download failed: " + pdfEx.Message); }
                        }

                        // 8. Store results + flip to stage 5.
                        UpsertStagingResult(connStr, claimId, slNo, disease, "done",
                            jobId, analysisJson, benefitPlan, pdfBytes, null);
                        SetClaimStage(connStr, claimId, 5);
                        processed++;
                        details.Add(new { claimId, disease, status = "done", jobId });
                    }
                    catch (Exception claimEx)
                    {
                        // Fail-open: mark failed, send to stage 5 for on-demand processing.
                        try
                        {
                            UpsertStagingResult(connStr, claimId, slNo: 1, diseaseType: null,
                                status: "failed", jobId: null, analysisJson: null,
                                benefitPlan: null, pdfBytes: null, lastError: claimEx.Message);
                            SetClaimStage(connStr, claimId, 5);
                        }
                        catch { /* swallow — never let one claim break the batch */ }
                        failed++;
                        details.Add(new { claimId, status = "failed", error = claimEx.Message });
                        TariffLog("[Staging] Claim " + claimId + " failed: " + claimEx.Message);
                    }
                }

                return Json(new
                {
                    Success = true,
                    Processed = processed,
                    Skipped = skipped,
                    Failed = failed,
                    Total = claimIds.Count,
                    Details = details
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { Success = false, Message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // ── Staging helper: resolve the raw SQL connection string ─────────────
        private string GetStagingConnString()
        {
            string connStr = System.Configuration.ConfigurationManager
                .ConnectionStrings["McarePlusEntities"]?.ConnectionString ?? "";
            if (connStr.StartsWith("metadata=", StringComparison.OrdinalIgnoreCase))
            {
                var m = System.Text.RegularExpressions.Regex.Match(
                    connStr, "provider connection string=\\\"([^\\\"]+)\\\"",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (m.Success) connStr = m.Groups[1].Value.Replace("&quot;", "\"");
            }
            return connStr;
        }

        // ── Staging helper: pull { Success, Data.base64Content, Data.fileName }
        //    out of the JsonResult returned by GetMedicalBillDocument/GetTariffDocument.
        private void ExtractDocBase64(ActionResult result, out string base64, out string fileName)
        {
            base64 = null; fileName = null;
            var jr = result as JsonResult;
            if (jr == null || jr.Data == null) return;
            try
            {
                dynamic d = jr.Data;
                bool ok = (bool?)d.Success ?? false;
                if (!ok || d.Data == null) return;
                base64   = (string)d.Data.base64Content;
                fileName = (string)d.Data.fileName;
            }
            catch { /* shape mismatch -> leave null */ }
        }

        // ── Staging helper: insert/update the ClaimAI_Results row for a claim ──
        private void UpsertStagingResult(
            string connStr, long claimId, int slNo, string diseaseType, string status,
            string jobId, string analysisJson, string benefitPlan, byte[] pdfBytes, string lastError)
        {
            using (var conn = new System.Data.SqlClient.SqlConnection(connStr))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    MERGE dbo.ClaimAI_Results AS t
                    USING (SELECT @ClaimID AS ClaimID, @SlNo AS SlNo) AS s
                       ON (t.ClaimID = s.ClaimID AND t.SlNo = s.SlNo)
                    WHEN MATCHED THEN UPDATE SET
                        DiseaseType      = COALESCE(@DiseaseType, t.DiseaseType),
                        ProcessingStatus = @Status,
                        ClaimAI_JobId    = COALESCE(@JobId, t.ClaimAI_JobId),
                        AnalysisJson     = COALESCE(@AnalysisJson, t.AnalysisJson),
                        BenefitPlan      = COALESCE(@BenefitPlan, t.BenefitPlan),
                        ProcessedPdf     = COALESCE(@ProcessedPdf, t.ProcessedPdf),
                        AttemptCount     = t.AttemptCount + 1,
                        LastError        = @LastError,
                        SubmittedAt      = CASE WHEN @Status = 'processing' THEN GETDATE() ELSE t.SubmittedAt END,
                        ProcessedAt      = CASE WHEN @Status IN ('done','failed','skipped') THEN GETDATE() ELSE t.ProcessedAt END
                    WHEN NOT MATCHED THEN INSERT
                        (ClaimID, SlNo, DiseaseType, ProcessingStatus, ClaimAI_JobId,
                         AnalysisJson, BenefitPlan, ProcessedPdf, AttemptCount, LastError,
                         CreatedAt, SubmittedAt, ProcessedAt)
                    VALUES
                        (@ClaimID, @SlNo, @DiseaseType, @Status, @JobId,
                         @AnalysisJson, @BenefitPlan, @ProcessedPdf, 1, @LastError,
                         GETDATE(),
                         CASE WHEN @Status = 'processing' THEN GETDATE() ELSE NULL END,
                         CASE WHEN @Status IN ('done','failed','skipped') THEN GETDATE() ELSE NULL END);";

                cmd.Parameters.AddWithValue("@ClaimID", claimId);
                cmd.Parameters.AddWithValue("@SlNo", slNo);
                cmd.Parameters.AddWithValue("@DiseaseType", (object)diseaseType ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Status", (object)status ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@JobId", (object)jobId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@AnalysisJson", (object)analysisJson ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@BenefitPlan", (object)benefitPlan ?? DBNull.Value);
                var pdfParam = cmd.Parameters.Add("@ProcessedPdf", System.Data.SqlDbType.VarBinary, -1);
                pdfParam.Value = (object)pdfBytes ?? DBNull.Value;
                cmd.Parameters.AddWithValue("@LastError",
                    (object)(lastError != null && lastError.Length > 2000 ? lastError.Substring(0, 2000) : lastError) ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        // ── Staging helper: flip a claim's stage ──────────────────────────────
        private void SetClaimStage(string connStr, long claimId, int stageId)
        {
            using (var conn = new System.Data.SqlClient.SqlConnection(connStr))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE Claims SET StageID = @stage WHERE ID = @cid";
                cmd.Parameters.AddWithValue("@stage", stageId);
                cmd.Parameters.AddWithValue("@cid", claimId);
                cmd.ExecuteNonQuery();
            }
        }

        // ── Staging helper: submit docs to ClaimAI /api/audit/start (synchronous),
        //    reusing the multipart mechanics from StartClaimAuditProxy. Returns jobId.
        private string SubmitClaimToClaimAI(
            string claimId, string billName, string billB64,
            string tarName, string tarB64, string disease)
        {
            byte[] medBytes = Convert.FromBase64String(billB64);
            byte[] tarBytes = null;
            if (!string.IsNullOrWhiteSpace(tarB64))
            {
                try { tarBytes = Convert.FromBase64String(tarB64); } catch { }
            }

            // Minimal spectraFields — the browser builds these from the DOM, but the
            // worker has none. Pass claimType; ClaimAI fills the rest from its own DB.
            string spectraFieldsJson = Newtonsoft.Json.JsonConvert.SerializeObject(new { claimType = disease });

            string claimAiUrl = (System.Configuration.ConfigurationManager.AppSettings["ClaimAIUrl"]
                ?? "http://localhost:3000").TrimEnd('/') + "/api/audit/start";

            System.Net.ServicePointManager.SecurityProtocol =
                System.Net.SecurityProtocolType.Tls12 |
                System.Net.SecurityProtocolType.Tls11 |
                System.Net.SecurityProtocolType.Tls;
            System.Net.ServicePointManager.ServerCertificateValidationCallback =
                (sender, cert, chain, errors) => true;

            string boundary = "ClaimAIBoundary" + Guid.NewGuid().ToString("N");
            byte[] multipartBody = BuildMultipartBody(
                boundary, claimId, billName ?? "medical-bill.pdf", medBytes,
                tarName, tarBytes, spectraFieldsJson);

            using (var client = new System.Net.Http.HttpClient())
            {
                client.Timeout = TimeSpan.FromMinutes(3);
                var req = new System.Net.Http.HttpRequestMessage(
                    System.Net.Http.HttpMethod.Post, claimAiUrl);
                var bodyContent = new System.Net.Http.ByteArrayContent(multipartBody);
                bodyContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(
                    "multipart/form-data; boundary=" + boundary);
                req.Content = bodyContent;

                var resp = client.SendAsync(req).GetAwaiter().GetResult();
                string respBody = resp.Content.ReadAsStringAsync().Result;
                if (!resp.IsSuccessStatusCode)
                    throw new Exception("ClaimAI audit/start HTTP " + (int)resp.StatusCode + ": " + respBody);

                dynamic convexRes = Newtonsoft.Json.JsonConvert.DeserializeObject(respBody);
                return convexRes?.jobId?.ToString() ?? "";
            }
        }

        // ── Staging helper: GET /api/staging/result?jobId= ────────────────────
        private void GetStagingResultFromClaimAI(
            string jobId, out string status, out string analysisJson,
            out string benefitPlan, out string processedPdfUrl)
        {
            status = null; analysisJson = null; benefitPlan = null; processedPdfUrl = null;

            string baseUrl = (System.Configuration.ConfigurationManager.AppSettings["ClaimAIUrl"]
                ?? "http://localhost:3000").TrimEnd('/');
            string url = baseUrl + "/api/staging/result?jobId=" + Uri.EscapeDataString(jobId);

            using (var client = new System.Net.Http.HttpClient())
            {
                client.Timeout = TimeSpan.FromMinutes(2);
                var resp = client.GetAsync(url).GetAwaiter().GetResult();
                string respBody = resp.Content.ReadAsStringAsync().Result;
                if (!resp.IsSuccessStatusCode)
                    throw new Exception("staging/result HTTP " + (int)resp.StatusCode + ": " + respBody);

                dynamic r = Newtonsoft.Json.JsonConvert.DeserializeObject(respBody);
                status = r?.status?.ToString();
                // analysis is a JSON object — re-serialize it to store as text.
                if (r?.analysis != null)
                    analysisJson = Newtonsoft.Json.JsonConvert.SerializeObject(r.analysis);
                benefitPlan     = r?.benefitPlan?.ToString();
                processedPdfUrl = r?.processedPdfUrl?.ToString();
            }
        }

        // ── Staging helper: download bytes from a URL (the processed PDF) ──────
        private byte[] DownloadBytes(string url)
        {
            System.Net.ServicePointManager.SecurityProtocol =
                System.Net.SecurityProtocolType.Tls12 |
                System.Net.SecurityProtocolType.Tls11 |
                System.Net.SecurityProtocolType.Tls;
            using (var client = new System.Net.Http.HttpClient())
            {
                client.Timeout = TimeSpan.FromMinutes(2);
                return client.GetByteArrayAsync(url).GetAwaiter().GetResult();
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // STAGING — STEP 6: claim-open lookup.
        //
        // GET /MedicalScrutiny/GetStagingResultForClaim?claimId=123
        //
        // Called by the claim page on load. If the claim was already pre-processed
        // by staging (ProcessingStatus = 'done' with a jobId), the page can render
        // the AI summary instantly from that jobId instead of running the on-demand
        // InitAiSummary flow.
        //
        // Returns:
        //   { Success, HasResult, Status, JobId, DiseaseType }
        //     HasResult = true only when Status = 'done' AND a jobId exists.
        //   Any other status (processing / failed / skipped / none) -> HasResult=false,
        //   and the page falls back to on-demand processing (today's behaviour).
        // ════════════════════════════════════════════════════════════════════
        [HttpGet]
        public ActionResult GetStagingResultForClaim(string claimId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(claimId) ||
                    !long.TryParse(claimId.Trim(), out long cId))
                    return Json(new { Success = true, HasResult = false }, JsonRequestBehavior.AllowGet);

                string connStr = GetStagingConnString();
                if (string.IsNullOrWhiteSpace(connStr))
                    return Json(new { Success = true, HasResult = false }, JsonRequestBehavior.AllowGet);

                string status = null, jobId = null, disease = null;
                using (var conn = new System.Data.SqlClient.SqlConnection(connStr))
                {
                    conn.Open();
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        SELECT TOP 1 ProcessingStatus, ClaimAI_JobId, DiseaseType
                        FROM dbo.ClaimAI_Results WITH (NOLOCK)
                        WHERE ClaimID = @cid
                        ORDER BY ID DESC";
                    cmd.Parameters.AddWithValue("@cid", cId);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (rdr.Read())
                        {
                            status  = rdr["ProcessingStatus"] == DBNull.Value ? null : rdr["ProcessingStatus"].ToString();
                            jobId   = rdr["ClaimAI_JobId"]    == DBNull.Value ? null : rdr["ClaimAI_JobId"].ToString();
                            disease = rdr["DiseaseType"]      == DBNull.Value ? null : rdr["DiseaseType"].ToString();
                        }
                    }
                }

                bool hasResult = (status == "done") && !string.IsNullOrWhiteSpace(jobId);
                return Json(new
                {
                    Success = true,
                    HasResult = hasResult,
                    Status = status,
                    JobId = jobId,
                    DiseaseType = disease
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                // On any error, report no result so the page falls back to on-demand.
                return Json(new { Success = false, HasResult = false, Message = ex.Message },
                    JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// GET /MedicalScrutiny/GetCodingProcedureEligibleLimit
        /// Called by ClaimAI to get the exact DB-calculated benefit plan limit
        /// using USP_Codingprocedurelimits with the claim's current coding data.
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        [OverrideAuthorization]
        public ActionResult GetCodingProcedureEligibleLimit(
            string claimId, string slNo = "1",
            string providerID = "0", string policyID = "0", string memberPolicyID = "0",
            string issueID = "0", string corpID = "0", string payerID = "0",
            string brokerID = "0", string siTypeID = "0", string claimType = "cataract")
        {
            try
            {
                long claimIdLong;
                if (!long.TryParse((claimId ?? "").Trim(), out claimIdLong) || claimIdLong <= 0)
                    return Json(new { success = false, error = "Invalid claimId" }, JsonRequestBehavior.AllowGet);

                string connStr = System.Configuration.ConfigurationManager
                                       .ConnectionStrings["McarePlusEntities"]?.ConnectionString ?? "";
                if (connStr.StartsWith("metadata=", StringComparison.OrdinalIgnoreCase))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(
                        connStr, @"provider connection string=""([^""]+)""",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (m.Success) connStr = m.Groups[1].Value.Replace("&quot;", "\"");
                }

                using (var conn = new System.Data.SqlClient.SqlConnection(connStr))
                {
                    conn.Open();

                    // Parse parameters passed from Spectra hidden fields
                    long provId   = long.TryParse(providerID,    out long _p)   ? _p   : 0;
                    long polId    = long.TryParse(policyID,      out long _po)  ? _po  : 0;
                    long memPolId = long.TryParse(memberPolicyID,out long _mp)  ? _mp  : 0;
                    int  issId    = int.TryParse(issueID,        out int  _i)   ? _i   : 0;
                    long corpId   = long.TryParse(corpID,        out long _c)   ? _c   : 0;
                    long payId    = long.TryParse(payerID,       out long _pa)  ? _pa  : 0;
                    int  brokId   = int.TryParse(brokerID,       out int  _b)   ? _b   : 0;
                    int  siTypId  = int.TryParse(siTypeID,       out int  _s)   ? _s   : 0;
                    int  slNoInt  = int.TryParse(slNo,           out int  _sl)  ? _sl  : 1;
                    byte isPED = 0, isGIPSA = 0, isCI = 0;
                    // Maternity = inpatient (isDayCare=0). Cataract = daycare (isDayCare=1)
                    byte isDayCare = (claimType == "maternity") ? (byte)0 : (byte)1;
                    int  procedureID = 0, level1 = 0;

                    // Get procedure ID from ClaimsCoding (most recent entry)
                    using (var cmd = new System.Data.SqlClient.SqlCommand(
                        @"SELECT TOP 1 TPAProcedureID, TPALevel1
                          FROM ClaimsCoding WITH(NOLOCK)
                          WHERE ClaimID = @ClaimID AND ISNULL(Deleted,0)=0
                          ORDER BY ID DESC", conn))
                    {
                        cmd.Parameters.AddWithValue("@ClaimID", claimIdLong);
                        using (var rdr = cmd.ExecuteReader())
                        {
                            if (rdr.Read())
                            {
                                procedureID = rdr["TPAProcedureID"] != DBNull.Value ? Convert.ToInt32(rdr["TPAProcedureID"]) : 0;
                                level1      = rdr["TPALevel1"]      != DBNull.Value ? Convert.ToInt32(rdr["TPALevel1"])      : 0;
                            }
                        }
                    }

                    if (memPolId == 0)
                        return Json(new { success = false, error = "Member policy not found." }, JsonRequestBehavior.AllowGet);

                    // If coding not done yet — skip SP, go directly to BPSIConditions fallback
                    if (procedureID == 0)
                    {
                        long bpsiIdFallback = 0;
                        using (var bpCmd = new System.Data.SqlClient.SqlCommand(
                            @"SELECT TOP 1 BPSIID FROM MemberSI WITH(NOLOCK)
                              WHERE MemberPolicyID = @MemberPolicyID
                              AND ISNULL(Deleted,0)=0
                              ORDER BY ID DESC", conn))
                        {
                            bpCmd.Parameters.AddWithValue("@MemberPolicyID", memPolId);
                            var val = bpCmd.ExecuteScalar();
                            if (val != null && val != DBNull.Value)
                                bpsiIdFallback = Convert.ToInt64(val);
                        }

                        if (bpsiIdFallback > 0)
                        {
                            double? ailmentLimitFb = null;
                            string  ailmentRuleFb  = "", ailmentRemarkFb = "";
                            using (var bpCmd = new System.Data.SqlClient.SqlCommand(
                                @"SELECT TOP 1 bsc.ClaimLimit, c.Name AS ConditionName, bsc.Remarks
                                  FROM BPSIConditions bsc WITH(NOLOCK)
                                  LEFT JOIN Mst_BPConditions c   WITH(NOLOCK) ON c.ID   = bsc.BPConditionID
                                  LEFT JOIN Mst_BPConditions par WITH(NOLOCK) ON par.ID = c.ParentID
                                  WHERE bsc.BPSIID = @BPSIID
                                  AND ISNULL(bsc.Deleted,0) = 0
                                  AND par.Name = 'Ailment Conditions'
                                  AND bsc.ClaimLimit IS NOT NULL
                                  AND ISNULL(bsc.isCovered,0) = 1
                                  ORDER BY bsc.ClaimLimit ASC", conn))
                            {
                                bpCmd.Parameters.AddWithValue("@BPSIID", bpsiIdFallback);
                                using (var rdr = bpCmd.ExecuteReader())
                                {
                                    if (rdr.Read())
                                    {
                                        ailmentLimitFb  = rdr["ClaimLimit"]    != DBNull.Value ? Convert.ToDouble(rdr["ClaimLimit"]) : (double?)null;
                                        ailmentRuleFb   = rdr["ConditionName"] != DBNull.Value ? rdr["ConditionName"].ToString()     : "";
                                        ailmentRemarkFb = rdr["Remarks"]       != DBNull.Value ? rdr["Remarks"].ToString()           : "";
                                    }
                                }
                            }

                            if (ailmentLimitFb.HasValue)
                                return Json(new {
                                    success        = true,
                                    noLimit        = false,
                                    eligibleAmount = ailmentLimitFb.Value,
                                    ruleName       = ailmentRuleFb,
                                    remarks        = ailmentRemarkFb,
                                    source         = "BPSIConditions (Ailment Cappings)",
                                    warning        = "Coding not completed. Showing Ailment Cappings limit — code the procedure for a more specific limit."
                                }, JsonRequestBehavior.AllowGet);
                        }

                        return Json(new {
                            success   = true,
                            noLimit   = true,
                            ruleName  = "Coding not completed. No benefit plan limit available.",
                            eligibleAmount = (double?)null
                        }, JsonRequestBehavior.AllowGet);
                    }

                    // Call USP_Codingprocedurelimits
                    var ds = new System.Data.DataSet();
                    using (var cmd = new System.Data.SqlClient.SqlCommand("USP_Codingprocedurelimits", conn))
                    {
                        cmd.CommandType    = System.Data.CommandType.StoredProcedure;
                        cmd.CommandTimeout = 120;
                        cmd.Parameters.AddWithValue("@ProviderID",     provId);
                        cmd.Parameters.AddWithValue("@ProcedureID",    procedureID);
                        cmd.Parameters.AddWithValue("@TPAProcID",      procedureID.ToString());
                        cmd.Parameters.AddWithValue("@TPAProcedureID", level1.ToString());
                        cmd.Parameters.AddWithValue("@IssueID",        issId);
                        cmd.Parameters.AddWithValue("@CorpID",         corpId);
                        cmd.Parameters.AddWithValue("@PayerID",        payId);
                        cmd.Parameters.AddWithValue("@PolicyID",       polId);
                        cmd.Parameters.AddWithValue("@ClaimID",        claimIdLong);
                        cmd.Parameters.AddWithValue("@MemberPolicyID", memPolId);
                        cmd.Parameters.AddWithValue("@SITypeID",       siTypId);
                        cmd.Parameters.AddWithValue("@isPED",          isPED);
                        cmd.Parameters.AddWithValue("@isGIPSA",        isGIPSA);
                        cmd.Parameters.AddWithValue("@isDaycare",      isDayCare);
                        cmd.Parameters.AddWithValue("@isCI",           isCI);
                        if (brokId != 0)
                            cmd.Parameters.AddWithValue("@BrokerID",   brokId);
                        cmd.Parameters.AddWithValue("@Slno",           slNoInt);

                        using (var adapter = new System.Data.SqlClient.SqlDataAdapter(cmd))
                            adapter.Fill(ds);
                    }

                    // Log to file
                    try {
                        string logDir  = System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data/Logs");
                        if (!System.IO.Directory.Exists(logDir)) System.IO.Directory.CreateDirectory(logDir);
                        string logFile = System.IO.Path.Combine(logDir, "CodingLimit_" + DateTime.Now.ToString("yyyyMMdd") + ".log");
                        var sb = new System.Text.StringBuilder();
                        sb.AppendLine($"=== USP_Codingprocedurelimits ===");
                        sb.AppendLine($"  ClaimID={claimIdLong} ProcID={procedureID} Level1={level1} MemPolID={memPolId} IssID={issId}");
                        sb.AppendLine($"  Tables={ds.Tables.Count}");
                        for (int t = 0; t < ds.Tables.Count; t++)
                            sb.AppendLine($"  Table[{t}] rows={ds.Tables[t].Rows.Count}");
                        System.IO.File.AppendAllText(logFile, DateTime.Now.ToString("HH:mm:ss") + " " + sb.ToString());
                    } catch { }

                    // Table2 = configured limits, Table3 = utilized amounts
                    System.Data.DataRow limitsRow = null;
                    bool spNoLimit = ds.Tables.Count < 2 || ds.Tables[1].Rows.Count == 0;
                    if (!spNoLimit)
                    {
                        limitsRow = ds.Tables[1].Rows[0];
                        spNoLimit = limitsRow["ClaimLimit"]     == DBNull.Value
                                 && limitsRow["IndividualLimit"] == DBNull.Value
                                 && limitsRow["FamilyLimit"]     == DBNull.Value
                                 && limitsRow["PolicyLimit"]     == DBNull.Value
                                 && limitsRow["CorporateLimit"]  == DBNull.Value;
                    }

                    if (spNoLimit)
                    {
                        // SP returned no limits — fall back to BPSIConditions Ailment Cappings
                        double? ailmentLimit = null;
                        string  ailmentRule  = "";
                        string  ailmentRemark = "";

                        // Get BPSIID from MemberSI
                        long bpsiId = 0;
                        using (var bpCmd = new System.Data.SqlClient.SqlCommand(
                            @"SELECT TOP 1 BPSIID FROM MemberSI WITH(NOLOCK)
                              WHERE MemberPolicyID = @MemberPolicyID
                              AND ISNULL(Deleted,0)=0
                              ORDER BY ID DESC", conn))
                        {
                            bpCmd.Parameters.AddWithValue("@MemberPolicyID", memPolId);
                            var val = bpCmd.ExecuteScalar();
                            if (val != null && val != DBNull.Value)
                                bpsiId = Convert.ToInt64(val);
                        }

                        if (bpsiId > 0)
                        {
                            using (var bpCmd = new System.Data.SqlClient.SqlCommand(
                                @"SELECT TOP 1 bsc.ClaimLimit, c.Name AS ConditionName, bsc.Remarks
                                  FROM BPSIConditions bsc WITH(NOLOCK)
                                  LEFT JOIN Mst_BPConditions c   WITH(NOLOCK) ON c.ID   = bsc.BPConditionID
                                  LEFT JOIN Mst_BPConditions par WITH(NOLOCK) ON par.ID = c.ParentID
                                  WHERE bsc.BPSIID = @BPSIID
                                  AND ISNULL(bsc.Deleted,0) = 0
                                  AND par.Name = 'Ailment Conditions'
                                  AND bsc.ClaimLimit IS NOT NULL
                                  AND ISNULL(bsc.isCovered,0) = 1
                                  ORDER BY bsc.ClaimLimit ASC", conn))
                            {
                                bpCmd.Parameters.AddWithValue("@BPSIID", bpsiId);
                                using (var rdr = bpCmd.ExecuteReader())
                                {
                                    if (rdr.Read())
                                    {
                                        ailmentLimit  = rdr["ClaimLimit"]     != DBNull.Value ? Convert.ToDouble(rdr["ClaimLimit"]) : (double?)null;
                                        ailmentRule   = rdr["ConditionName"]  != DBNull.Value ? rdr["ConditionName"].ToString()     : "";
                                        ailmentRemark = rdr["Remarks"]        != DBNull.Value ? rdr["Remarks"].ToString()           : "";
                                    }
                                }
                            }
                        }

                        if (ailmentLimit.HasValue)
                            return Json(new {
                                success        = true,
                                noLimit        = false,
                                eligibleAmount = ailmentLimit.Value,
                                ruleName       = ailmentRule,
                                remarks        = ailmentRemark,
                                source         = "BPSIConditions (Ailment Cappings)",
                                warning        = "Procedure not linked in benefit plan. Amount shown is from Ailment Cappings — verify before approving."
                            }, JsonRequestBehavior.AllowGet);

                        return Json(new {
                            success        = true,
                            noLimit        = true,
                            ruleName       = "No ailment sub-limit configured — full sum insured applies",
                            eligibleAmount = (double?)null
                        }, JsonRequestBehavior.AllowGet);
                    }

                    string ruleName = limitsRow["RuleName"] != DBNull.Value ? limitsRow["RuleName"].ToString() : "";
                    double claimLim = limitsRow["ClaimLimit"]      != DBNull.Value ? Convert.ToDouble(limitsRow["ClaimLimit"])      : double.MaxValue;
                    double indLim   = limitsRow["IndividualLimit"]  != DBNull.Value ? Convert.ToDouble(limitsRow["IndividualLimit"]) : double.MaxValue;
                    double famLim   = limitsRow["FamilyLimit"]      != DBNull.Value ? Convert.ToDouble(limitsRow["FamilyLimit"])     : double.MaxValue;
                    double polLim   = limitsRow["PolicyLimit"]      != DBNull.Value ? Convert.ToDouble(limitsRow["PolicyLimit"])     : double.MaxValue;
                    double corpLim  = limitsRow["CorporateLimit"]   != DBNull.Value ? Convert.ToDouble(limitsRow["CorporateLimit"])  : double.MaxValue;

                    double utilClaim = 0, utilInd = 0, utilFam = 0, utilPol = 0, utilCorp = 0;
                    if (ds.Tables.Count >= 3 && ds.Tables[2].Rows.Count > 0)
                    {
                        var u    = ds.Tables[2].Rows[0];
                        utilClaim = u["ClaimLimit"]      != DBNull.Value ? Convert.ToDouble(u["ClaimLimit"])      : 0;
                        utilInd   = u["IndividualLimit"]  != DBNull.Value ? Convert.ToDouble(u["IndividualLimit"]) : 0;
                        utilFam   = u["FamilyLimit"]      != DBNull.Value ? Convert.ToDouble(u["FamilyLimit"])     : 0;
                        utilPol   = u["PolicyLimit"]      != DBNull.Value ? Convert.ToDouble(u["PolicyLimit"])     : 0;
                        utilCorp  = u["CorporateLimit"]   != DBNull.Value ? Convert.ToDouble(u["CorporateLimit"])  : 0;
                    }

                    var candidates = new System.Collections.Generic.List<double>();
                    if (claimLim < double.MaxValue) candidates.Add(Math.Max(0, claimLim - utilClaim));
                    if (indLim   < double.MaxValue) candidates.Add(Math.Max(0, indLim   - utilInd));
                    if (famLim   < double.MaxValue) candidates.Add(Math.Max(0, famLim   - utilFam));
                    if (polLim   < double.MaxValue) candidates.Add(Math.Max(0, polLim   - utilPol));
                    if (corpLim  < double.MaxValue) candidates.Add(Math.Max(0, corpLim  - utilCorp));

                    double eligibleAmount = candidates.Count > 0 ? candidates.Min() : 0;

                    return Json(new {
                        success        = true,
                        noLimit        = false,
                        eligibleAmount = eligibleAmount,
                        ruleName       = ruleName,
                        limits = new {
                            claimLimit      = claimLim < double.MaxValue ? (object)claimLim : null,
                            individualLimit = indLim   < double.MaxValue ? (object)indLim   : null,
                            familyLimit     = famLim   < double.MaxValue ? (object)famLim   : null,
                            policyLimit     = polLim   < double.MaxValue ? (object)polLim   : null,
                            corporateLimit  = corpLim  < double.MaxValue ? (object)corpLim  : null
                        },
                        utilized = new {
                            claimLimit = utilClaim, individualLimit = utilInd,
                            familyLimit = utilFam,  policyLimit = utilPol, corporateLimit = utilCorp
                        }
                    }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

                /// <summary>
        /// Writes a tariff selection log entry to a dedicated log file.
        /// </summary>
        private static void TariffLog(string message)
        {
            try
            {
                string logDir  = System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data/Logs");
                if (!System.IO.Directory.Exists(logDir))
                    System.IO.Directory.CreateDirectory(logDir);
                string logFile = System.IO.Path.Combine(logDir, "TariffSelection_" + DateTime.Now.ToString("yyyyMMdd") + ".log");
                string line    = DateTime.Now.ToString("HH:mm:ss.fff") + " " + message + Environment.NewLine;
                System.IO.File.AppendAllText(logFile, line);
            }
            catch { /* never let logging break the main flow */ }
        }

        /// <summary>
        /// Picks the best tariff file using AI. Falls back to rule-based PickBestTariffFile if AI fails.
        /// </summary>
        // Holds a human-readable summary of the most recent tariff file
        // selection (candidate filenames sent to ClaimAI + the selection
        // received). Populated by PickTariffFileWithAI and appended to the
        // GetTariffDocument response Message so it surfaces in the Spectra
        // browser console when AI Summary is opened. Cross-check only.
        private string _lastTariffSelectionLog = "";

        // Set true by the staging worker (ProcessStagingClaims) while it calls
        // GetMedicalBillDocument / GetTariffDocument internally, so those methods
        // skip the user-session gate (the worker has no session). Always reset to
        // false in a finally block. Browser-initiated calls never set it.
        private bool _stagingInternalCall = false;

        private System.Tuple<string, byte[]> PickTariffFileWithAI(
            System.Collections.Generic.List<System.Tuple<string, DateTime, byte[]>> candidates,
            bool isPsu, string insurerCode)
        {
            _lastTariffSelectionLog = "";
            if (candidates == null || candidates.Count == 0) {
                _lastTariffSelectionLog = "Sent 0 files (no tariff candidates). insurerCode=" + (insurerCode ?? "") + " isPsu=" + isPsu;
                return null;
            }
            if (candidates.Count == 1)
            {
                _lastTariffSelectionLog = "Sent 1 file: [" + candidates[0].Item1 + "] | insurerCode=" + (insurerCode ?? "") + " isPsu=" + isPsu + " -> selected: " + candidates[0].Item1 + " (only one file, no AI call)";
                byte[] converted = EnsurePdf(candidates[0].Item1, candidates[0].Item3);
                return converted != null ? System.Tuple.Create(candidates[0].Item1, converted) : null;
            }

            try
            {
                string claimAiUrl = System.Configuration.ConfigurationManager.AppSettings["claimAIUrl"] ?? "";
                // Strip any path — we only need the base (scheme + host + port)
                if (!string.IsNullOrWhiteSpace(claimAiUrl))
                {
                    try
                    {
                        var u = new System.Uri(claimAiUrl.TrimEnd('/'));
                        claimAiUrl = u.GetLeftPart(System.UriPartial.Authority);
                    }
                    catch { /* keep as-is if parsing fails */ }
                }
                TariffLog("[Tariff] AI CONFIG — ClaimAI base URL='" + claimAiUrl + "'");
                if (!string.IsNullOrWhiteSpace(claimAiUrl))
                {
                    string baseUrl = claimAiUrl; // already stripped to base URL above

                    var fileNames = candidates.ConvertAll(c => c.Item1);
                    _lastTariffSelectionLog = "Sent " + fileNames.Count + " files: [" + string.Join(" | ", fileNames) + "] | insurerCode=" + (insurerCode ?? "") + " isPsu=" + isPsu;
                    TariffLog("[Tariff] AI INPUT — Calling: " + baseUrl + "/api/tariff-file-selection");
                    TariffLog("[Tariff] AI INPUT — " + fileNames.Count + " files: " + string.Join(" | ", fileNames));
                    TariffLog("[Tariff] AI INPUT — InsurerCode=" + insurerCode + " IsPSU=" + isPsu);

                    var payload = Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        fileNames   = fileNames,
                        insurerCode = insurerCode ?? "",
                        isPsu       = isPsu
                    });

                    using (var http = new System.Net.Http.HttpClient())
                    {
                        http.Timeout = TimeSpan.FromSeconds(15);
                        var body = new System.Net.Http.StringContent(payload, System.Text.Encoding.UTF8, "application/json");
                        var response = http.PostAsync(baseUrl + "/api/tariff-file-selection", body).GetAwaiter().GetResult();

                        if (response.IsSuccessStatusCode)
                        {
                            var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            dynamic result = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
                            string selectedFile = result?.selectedFile?.ToString();

                            _lastTariffSelectionLog += " -> AI selected: " + (selectedFile ?? "null") + " | tier=" + (result?.priorityTier?.ToString() ?? "") + " | reason=" + (result?.reason?.ToString() ?? "");
                            TariffLog("[Tariff] AI OUTPUT — Selected: " + selectedFile + " | Tier: " + result?.priorityTier + " | Reason: " + result?.reason);

                            if (!string.IsNullOrWhiteSpace(selectedFile))
                            {
                                var match = candidates.Find(c => c.Item1 == selectedFile);
                                if (match != null)
                                {
                                    byte[] converted = EnsurePdf(match.Item1, match.Item3);
                                    if (converted != null) return System.Tuple.Create(match.Item1, converted);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TariffLog("[Tariff] AI CALL FAILED — " + ex.GetType().Name + ": " + ex.Message);
                _lastTariffSelectionLog += " -> AI call FAILED: " + ex.GetType().Name + ": " + ex.Message;
            }

            // Fallback: use existing rule-based logic
            _lastTariffSelectionLog += " -> FALLBACK: rule-based selection used";
            TariffLog("[Tariff] FALLBACK — AI failed, using rule-based PickBestTariffFile");
            var fb = PickBestTariffFile(candidates, isPsu, insurerCode);
            _lastTariffSelectionLog += " -> fallback selected: " + (fb?.Item1 ?? "null");
            return fb;
        }

        /// <summary>
        /// Ensures the file bytes are a valid PDF.
        /// If the file is Excel (.xlsx/.xls), converts it to PDF using ClosedXML + iTextSharp.
        /// Returns null if file cannot be read or converted.
        /// </summary>
        private byte[] EnsurePdf(string fileName, byte[] fileBytes)
        {
            if (fileBytes == null || fileBytes.Length == 0) return null;
            var nameLower = (fileName ?? "").ToLower();

            try
            {
                // Already a PDF — validate by checking header
                if (nameLower.EndsWith(".pdf"))
                {
                    // Quick PDF header check
                    if (fileBytes.Length > 4 &&
                        fileBytes[0] == 0x25 && fileBytes[1] == 0x50 &&
                        fileBytes[2] == 0x44 && fileBytes[3] == 0x46)
                        return fileBytes;
                    // Try reading with iTextSharp to validate
                    var reader = new iTextSharp.text.pdf.PdfReader(fileBytes);
                    reader.Close();
                    return fileBytes;
                }

                // Excel file — convert to PDF
                if (nameLower.EndsWith(".xlsx") || nameLower.EndsWith(".xls"))
                {
                    return ConvertExcelToPdf(fileBytes, nameLower.EndsWith(".xlsx"));
                }

                // Unknown type — try as PDF anyway
                return fileBytes;
            }
            catch
            {
                return null; // unreadable — skip this file
            }
        }

        /// <summary>
        /// Converts an Excel file (xlsx/xls) to a clean, readable PDF using ClosedXML + iTextSharp.
        /// Produces a properly formatted table with headers, alternating rows, and auto column widths.
        /// </summary>
        private byte[] ConvertExcelToPdf(byte[] excelBytes, bool isXlsx)
        {
            try
            {
                using (var excelStream = new System.IO.MemoryStream(excelBytes))
                using (var pdfStream = new System.IO.MemoryStream())
                {
                    var workbook = new ClosedXML.Excel.XLWorkbook(excelStream);

                    // Use A4 portrait — landscape causes text rotation issues in react-pdf
                    var pageSize = iTextSharp.text.PageSize.A4;
                    var document = new iTextSharp.text.Document(pageSize, 20f, 20f, 20f, 20f);
                    iTextSharp.text.pdf.PdfWriter.GetInstance(document, pdfStream);
                    document.Open();

                    // Fonts
                    var titleFont   = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 13, iTextSharp.text.BaseColor.WHITE);
                    var headerFont  = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 9,  iTextSharp.text.BaseColor.WHITE);
                    var cellFont    = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA,      9,  new iTextSharp.text.BaseColor(33, 33, 33));
                    var altCellFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA,      9,  new iTextSharp.text.BaseColor(33, 33, 33));

                    // Colors
                    var headerBg  = new iTextSharp.text.BaseColor(30,  58,  95);   // dark navy
                    var altRowBg  = new iTextSharp.text.BaseColor(240, 245, 255);  // light blue
                    var whiteBg   = iTextSharp.text.BaseColor.WHITE;
                    var borderCol = new iTextSharp.text.BaseColor(200, 210, 230);

                    bool firstSheet = true;
                    foreach (var worksheet in workbook.Worksheets)
                    {
                        if (worksheet.IsEmpty()) continue;
                        if (!firstSheet) document.NewPage();
                        firstSheet = false;

                        var usedRange = worksheet.RangeUsed();
                        if (usedRange == null) continue;

                        int colCount = usedRange.ColumnCount();
                        int rowCount = usedRange.RowCount();

                        // Sheet title bar
                        var titleTable = new iTextSharp.text.pdf.PdfPTable(1) { WidthPercentage = 100, SpacingAfter = 6f };
                        var titleCell  = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(worksheet.Name, titleFont))
                        {
                            BackgroundColor = headerBg,
                            Padding         = 8f,
                            Border          = iTextSharp.text.Rectangle.NO_BORDER,
                            HorizontalAlignment = iTextSharp.text.Element.ALIGN_LEFT
                        };
                        titleTable.AddCell(titleCell);
                        document.Add(titleTable);

                        // Compute relative column widths based on max content length
                        var colWidths = new float[colCount];
                        for (int col = 0; col < colCount; col++)
                        {
                            float maxLen = 4f;
                            for (int row = usedRange.FirstRow().RowNumber(); row <= usedRange.LastRow().RowNumber(); row++)
                            {
                                var cellVal = worksheet.Cell(row, usedRange.FirstColumn().ColumnNumber() + col).GetString();
                                if (cellVal.Length > maxLen) maxLen = cellVal.Length;
                            }
                            colWidths[col] = Math.Min(maxLen, 40f); // cap at 40 chars wide
                        }
                        var dataTable = new iTextSharp.text.pdf.PdfPTable(colCount)
                        {
                            WidthPercentage = 100,
                            SpacingAfter    = 12f
                        };
                        dataTable.SetWidths(colWidths);

                        int dataRow = 0;
                        for (int row = usedRange.FirstRow().RowNumber(); row <= usedRange.LastRow().RowNumber(); row++)
                        {
                            bool isHeaderRow = (dataRow == 0);
                            bool isAltRow    = (!isHeaderRow && dataRow % 2 == 0);
                            var rowBg = isHeaderRow ? headerBg : (isAltRow ? altRowBg : whiteBg);

                            for (int col = usedRange.FirstColumn().ColumnNumber(); col <= usedRange.LastColumn().ColumnNumber(); col++)
                            {
                                var excelCell = worksheet.Cell(row, col);
                                string val    = excelCell.IsEmpty() ? "" : excelCell.GetString();

                                var pdfCell = new iTextSharp.text.pdf.PdfPCell(
                                    new iTextSharp.text.Phrase(val, isHeaderRow ? headerFont : (isAltRow ? altCellFont : cellFont)))
                                {
                                    BackgroundColor     = rowBg,
                                    Padding             = 5f,
                                    BorderColor         = borderCol,
                                    BorderWidth         = 0.5f,
                                    HorizontalAlignment = isHeaderRow
                                        ? iTextSharp.text.Element.ALIGN_CENTER
                                        : iTextSharp.text.Element.ALIGN_LEFT,
                                    VerticalAlignment   = iTextSharp.text.Element.ALIGN_MIDDLE,
                                };
                                dataTable.AddCell(pdfCell);
                            }
                            dataRow++;
                        }

                        document.Add(dataTable);
                    }

                    document.Close();
                    return pdfStream.ToArray();
                }
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog.GetDefault(null).Log(new Elmah.Error(
                    new Exception("Excel to PDF conversion failed: " + ex.Message)));
                return null;
            }
        }

        /// <summary>
        /// Fetches medical bill PDF.
        /// Prod/Preprod/Live : DMS API → pre-signed S3 URLs → merge.
        /// Dev/QA/UAT        : ClaimAIDocs/{claimId}/medicalbill.zip → extract all PDFs (deduplicated) → merge.
        /// </summary>
        /// <summary>
        /// Hardcoded test claim IDs for which medical bill + tariff should be
        /// loaded from the local /ClaimAIDocs folder, even in prod/preprod.
        /// All other claims use the normal DMS/S3 flow based on Web.config Enviroment key.
        /// REMOVE THIS LIST once testing is done.
        /// </summary>
        private static readonly System.Collections.Generic.HashSet<string> _HardcodedTestClaimIds =
            new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "26051796629", "26051796637", "26051796654", "26051796671", "26051796762",
            "26051796423", "26051796577", "26051796489", "26051796501", "26051796596",
            "26051591856", "26051694326", "26051897945", "26051591854", "26051590845",
            "26051591486", "26051696018", "26051897809", "26051485174", "26051591399",
            "26051695562", "26051696051", "26051898316", "26051899125", "26051899333",
            "26051899932", "26051591758", "26051593677", "26051694447", "26051591708"
        };

        private static bool IsHardcodedTestClaim(string claimId)
        {
            return !string.IsNullOrWhiteSpace(claimId) &&
                   _HardcodedTestClaimIds.Contains(claimId.Trim());
        }

        public ActionResult GetMedicalBillDocument(string claimId = null, string slNo = null)
        {
            var res = new ApiResponse<object>();
            try
            {
                // Session check applies to browser-initiated calls only. The staging
                // worker (ProcessStagingClaims) calls this method internally with no
                // user session — _stagingInternalCall is set true for that path so we
                // skip the session gate. Browser calls leave it false → gate enforced.
                if (!_stagingInternalCall && Session[SessionValue.UserRegionID] == null)
                {
                    res.Success = false; res.ErrorCode = "ErrorCode#1";
                    res.Message = "Session expired.";
                    return Json(res, JsonRequestBehavior.AllowGet);
                }

                string cId = (claimId ?? "").Trim();
                string tarFileName = null; // will be set to actual picked file name
                string sNo = (slNo ?? "1").Trim();

                if (string.IsNullOrWhiteSpace(cId))
                {
                    res.Success = false;
                    res.Message = "ClaimID is required.";
                    return Json(res, JsonRequestBehavior.AllowGet);
                }

                string env = (System.Configuration.ConfigurationManager.AppSettings["Enviroment"] ?? "dev").ToLower().Trim();
                bool isProdOrPreprod = env == "prod" || env == "preprod" || env == "live";

                // TESTING OVERRIDE: hardcoded claim IDs always use local file path.
                // Remove the IsHardcodedTestClaim check once testing finishes.
                if (IsHardcodedTestClaim(cId))
                {
                    System.Diagnostics.Debug.WriteLine("[ClaimAI] Test claim override — using LOCAL file path for claimId=" + cId);
                    isProdOrPreprod = false;
                }

                if (!isProdOrPreprod)
                {
                    // CHECKPOINT 2a (Local/QA/UAT): Load medical bill from zip file
                    // Path: ~/ClaimAIDocs/{claimId}/medicalbill.zip
                    // FAILURE HERE means:
                    //   - Zip file not placed in correct folder on server
                    //   - ClaimAIDocs folder not created under Enrollment project root
                    //   - Wrong claimId passed
                    string localBase = Server.MapPath("~/ClaimAIDocs/");
                    string zipPath   = System.IO.Path.Combine(localBase, cId, "medicalbill.zip");

                    if (!System.IO.File.Exists(zipPath))
                    {
                        res.Success = false;
                        res.Message = "CHECKPOINT 2a FAILED: Medical bill zip not found at: " + zipPath
                            + ". Create folder ClaimAIDocs/" + cId + "/ and place medicalbill.zip inside it.";
                        return Json(res, JsonRequestBehavior.AllowGet);
                    }

                    // Extract all PDFs, deduplicate by content hash (MD5)
                    // Handles same content in different filenames
                    var pdfBytesList = new System.Collections.Generic.List<byte[]>();
                    var seenHashes   = new System.Collections.Generic.HashSet<string>();

                    using (var zip = System.IO.Compression.ZipFile.OpenRead(zipPath))
                    {
                        foreach (var entry in zip.Entries)
                        {
                            if (!entry.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) continue;
                            using (var stream = entry.Open())
                            using (var ms = new System.IO.MemoryStream())
                            {
                                stream.CopyTo(ms);
                                byte[] pdfBytes = ms.ToArray();

                                // Compute MD5 hash of file content
                                string hash;
                                using (var md5 = System.Security.Cryptography.MD5.Create())
                                    hash = BitConverter.ToString(md5.ComputeHash(pdfBytes)).Replace("-", "");

                                if (!seenHashes.Add(hash)) continue; // skip duplicate content
                                pdfBytesList.Add(pdfBytes);
                            }
                        }
                    }

                    if (pdfBytesList.Count == 0)
                    {
                        res.Success = false;
                        res.Message = "No PDFs found in medicalbill.zip for claimId=" + cId;
                        return Json(res, JsonRequestBehavior.AllowGet);
                    }

                    // Merge and compress all pages — no page cap
                    // Large files are handled by passing URL to AI directly (not loading into Convex action memory)
                    byte[] mergedLocal    = MergePdfs(pdfBytesList);
                    byte[] compressedLocal = CompressPdf(mergedLocal);
                    int totalPages = new iTextSharp.text.pdf.PdfReader(mergedLocal).NumberOfPages;
                    double sizeMb = Math.Round(compressedLocal.Length / 1048576.0, 2);
                    res.Success = true;
                    res.Message = "Medical bill loaded from zip. Files: " + pdfBytesList.Count + " | Pages: " + totalPages + " | Size: " + sizeMb + "MB";
                    res.Data    = new { fileName = cId + "-medicalbill.pdf", base64Content = Convert.ToBase64String(compressedLocal) };
                    var sl = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                    return Content(sl.Serialize(res), "application/json");
                }

                // ── Prod/Preprod/Live: DMS API ────────────────────────────────────────
                string dmsBaseUrl = System.Configuration.ConfigurationManager.AppSettings["DMSApiURL"].TrimEnd('/');
                string clientId   = System.Configuration.ConfigurationManager.AppSettings["ClientID"];
                string apiKey     = System.Configuration.ConfigurationManager.AppSettings["DMSAPIKey"];

                System.Net.ServicePointManager.SecurityProtocol =
                    System.Net.SecurityProtocolType.Tls12 |
                    System.Net.SecurityProtocolType.Tls11 |
                    System.Net.SecurityProtocolType.Tls;
                System.Net.ServicePointManager.ServerCertificateValidationCallback =
                    (sender, cert, chain, errors) => true;

                string token = "";
                {
                    var client  = new System.Net.Http.HttpClient();
                    client.Timeout = TimeSpan.FromSeconds(30);
                    var jsonDoc = Newtonsoft.Json.JsonConvert.SerializeObject(new { clientId, apiKey });
                    var request = new System.Net.Http.HttpRequestMessage(
                        System.Net.Http.HttpMethod.Post, dmsBaseUrl + "/api/Auth/generatetoken");
                    request.Content = new System.Net.Http.StringContent(jsonDoc, null, "application/json");
                    var response = client.SendAsync(request).GetAwaiter().GetResult();
                    if (response.IsSuccessStatusCode)
                        token = response.Content.ReadAsStringAsync().Result.Trim().Trim('"');
                }

                if (string.IsNullOrWhiteSpace(token))
                {
                    res.Success = false;
                    res.Message = "DMS token generation failed.";
                    return Json(res, JsonRequestBehavior.AllowGet);
                }

                string docsJson = "";
                {
                    var client = new System.Net.Http.HttpClient();
                    client.Timeout = TimeSpan.FromSeconds(30);
                    client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
                    client.DefaultRequestHeaders.Add("accept", "*/*");
                    string url = string.Format("{0}/api/Document/claimdocumenturls?claimId={1}&claimExtNo={2}",
                        dmsBaseUrl, cId, sNo);
                    var response = client.GetAsync(url).GetAwaiter().GetResult();
                    docsJson = response.Content.ReadAsStringAsync().Result;
                }

                if (string.IsNullOrWhiteSpace(docsJson) || !docsJson.TrimStart().StartsWith("["))
                {
                    res.Success = false;
                    res.Message = "No documents found in DMS for claimId=" + cId + ". Response: " + docsJson;
                    return Json(res, JsonRequestBehavior.AllowGet);
                }

                var docsList = Newtonsoft.Json.JsonConvert.DeserializeObject<List<dynamic>>(docsJson);
                if (docsList == null || docsList.Count == 0)
                {
                    res.Success = false;
                    res.Message = "Empty document list for claimId=" + cId;
                    return Json(res, JsonRequestBehavior.AllowGet);
                }

                var pdfBytesListProd = new System.Collections.Generic.List<byte[]>();
                foreach (var doc in docsList)
                {
                    string docUrl = (doc.documentUrl ?? doc.DocumentUrl ?? "").ToString();
                    if (string.IsNullOrWhiteSpace(docUrl)) continue;
                    try
                    {
                        using (var wc = new System.Net.WebClient())
                            pdfBytesListProd.Add(wc.DownloadData(docUrl));
                    }
                    catch { }
                }

                if (pdfBytesListProd.Count == 0)
                {
                    res.Success = false;
                    res.Message = "Could not download any documents from DMS for claimId=" + cId;
                    return Json(res, JsonRequestBehavior.AllowGet);
                }

                byte[] mergedProd = MergePdfs(pdfBytesListProd);
                res.Success = true;
                res.Message = "Medical bill loaded from DMS. Files: " + pdfBytesListProd.Count;
                res.Data    = new { fileName = cId + "-" + sNo + "-medical-bill.pdf", base64Content = Convert.ToBase64String(mergedProd) };
                var s = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                return Content(s.Serialize(res), "application/json");
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog.GetDefault(null).Log(new Elmah.Error(ex));
                res.Success = false;
                res.Message = "Error loading medical bill: " + ex.Message;
                return Json(res, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Fetches tariff PDF.
        /// Dev/QA/UAT        : ClaimAIDocs/{claimId}/tariff.zip
        ///                     → find best inner zip by rules → extract best PDF by rules.
        /// Prod/Preprod/Live : S3 {providerId}/TariffDocs/ → list files → pick by rules.
        /// Rules: PSU(5,6,7,8) priority: insurer-specific → GIPSA → GIPSA SOC → All Insurers
        ///        Private priority: insurer-specific → All Insurers/Pvt Insurers
        /// </summary>
        public ActionResult GetTariffDocument(string claimId = null, string slNo = null)
        {
            var res = new ApiResponse<object>();
            try
            {
                // See GetMedicalBillDocument: skip the session gate for internal
                // staging-worker calls (no user session); enforce it for browser calls.
                if (!_stagingInternalCall && Session[SessionValue.UserRegionID] == null)
                {
                    res.Success = false; res.ErrorCode = "ErrorCode#1";
                    res.Message = "Session expired.";
                    return Json(res, JsonRequestBehavior.AllowGet);
                }

                string cId = (claimId ?? "").Trim();
                string tarFileName = null; // will be set to actual picked file name

                // Get insurer info from DB
                string connStr = System.Configuration.ConfigurationManager
                    .ConnectionStrings["McarePlusEntities"].ConnectionString;
                if (connStr.StartsWith("metadata=", StringComparison.OrdinalIgnoreCase))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(
                        connStr, "provider connection string=\\\"([^\\\"]+)\\\"",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (m.Success) connStr = m.Groups[1].Value.Replace("&quot;", "\"");
                }

                int insurerId = 0;
                string insurerCode = "";
                if (!string.IsNullOrWhiteSpace(cId) && long.TryParse(cId, out long cIdLong))
                    GetClaimInsurerInfo(cIdLong, connStr, out insurerId, out insurerCode);

                bool isPsu = PsuInsurerIds.Contains(insurerId);

                string env = (System.Configuration.ConfigurationManager.AppSettings["Enviroment"] ?? "dev").ToLower().Trim();
                bool isProdOrPreprod = env == "prod" || env == "preprod" || env == "live";

                // TESTING OVERRIDE: hardcoded claim IDs always use local file path.
                // Remove the IsHardcodedTestClaim check once testing finishes.
                if (IsHardcodedTestClaim(cId))
                {
                    System.Diagnostics.Debug.WriteLine("[ClaimAI] Test claim override (tariff) — using LOCAL file path for claimId=" + cId);
                    isProdOrPreprod = false;
                }

                if (!isProdOrPreprod)
                {
                    // CHECKPOINT 3a (Local/QA/UAT): Load tariff from zip file
                    // Path: ~/ClaimAIDocs/{claimId}/tariff.zip
                    // FAILURE HERE means:
                    //   - Zip file not placed in correct folder
                    //   - Wrong claimId passed
                    //   - Enviroment key in Web.config is wrong (should be qa/dev/uat)
                    string localBase = Server.MapPath("~/ClaimAIDocs/");
                    string zipPath   = System.IO.Path.Combine(localBase, cId, "tariff.zip");

                    if (!System.IO.File.Exists(zipPath))
                    {
                        res.Success = false;
                        res.Message = "CHECKPOINT 3a FAILED: Tariff zip not found at: " + zipPath +
                            ". Create folder ClaimAIDocs/" + cId + "/ and place tariff.zip inside it.";
                        return Json(res, JsonRequestBehavior.AllowGet);
                    }

                    // Recursively collect ALL tariff files from the zip — at any
                    // nesting depth. Files inside inner zips carry the full chain
                    // of inner-zip names as a prefix (".zip" stripped, joined by
                    // "_"); the outer root zip is NOT part of the prefix.
                    //   tariff.zip/file.pdf                 -> "file.pdf"
                    //   tariff.zip/gipsa.zip/file.pdf       -> "gipsa_file.pdf"
                    //   tariff.zip/gipsa.zip/2024.zip/f.pdf -> "gipsa_2024_f.pdf"
                    var allTariffCandidates = new System.Collections.Generic.List<System.Tuple<string, DateTime, byte[]>>();
                    using (var rootStream = System.IO.File.OpenRead(zipPath))
                    {
                        CollectTariffFilesFromZip(rootStream, "", allTariffCandidates);
                    }

                    byte[] tariffBytes = null;

                    TariffLog("[Tariff] STEP 1 — Zip scanned recursively. Total tariff files=" + allTariffCandidates.Count);
                    TariffLog("[Tariff] STEP 1 — InsurerCode=" + insurerCode + " IsPSU=" + isPsu + " ClaimID=" + cId);

                    if (allTariffCandidates.Count > 0)
                    {
                        TariffLog("[Tariff] STEP 2 — All candidate file names collected (" + allTariffCandidates.Count + "): " + string.Join(" | ", allTariffCandidates.ConvertAll(f => f.Item1 + " [" + f.Item2.ToString("yyyy-MM-dd") + "]")));

                        // Use AI to pick best file (names now carry the inner-zip prefix chain)
                        TariffLog("[Tariff] STEP 3 — Sending to AI for selection...");
                        var aiPicked = PickTariffFileWithAI(allTariffCandidates, isPsu, insurerCode);
                        tariffBytes = aiPicked?.Item2;
                        tarFileName = aiPicked?.Item1 ?? tarFileName;
                        TariffLog("[Tariff] STEP 4 — AI selected: " + (tarFileName ?? "null"));
                    }

                    if (tariffBytes == null)
                    {
                        res.Success = false;
                        res.Message = "Could not pick tariff file from zip. InsurerID=" + insurerId + " Code=" + insurerCode;
                        return Json(res, JsonRequestBehavior.AllowGet);
                    }

                    res.Success = true;
                    res.Message = "Tariff loaded from zip. IsPSU=" + isPsu + " InsurerCode=" + insurerCode
                                + " || TARIFF SELECTION: " + _lastTariffSelectionLog;
                    res.Data    = new { fileName = tarFileName ?? (cId + "-tariff.pdf"), base64Content = Convert.ToBase64String(tariffBytes) };
                    var sl2 = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                    return Content(sl2.Serialize(res), "application/json");
                }

                // ── Prod/Preprod/Live: S3 via Usp_TariffUploadDoc_FillDetails ─────────
                long claimIdLong2 = 0;
                long.TryParse(cId, out claimIdLong2);

                int slNoInt = 1;
                using (var conn = new System.Data.SqlClient.SqlConnection(connStr))
                {
                    conn.Open();
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT TOP 1 Slno FROM Claimsdetails WHERE ClaimID=@cid AND ISNULL(Deleted,0)=0 ORDER BY Slno";
                    cmd.Parameters.AddWithValue("@cid", claimIdLong2);
                    var val = cmd.ExecuteScalar();
                    if (val != null && val != DBNull.Value) slNoInt = Convert.ToInt32(val);
                }

                long providerId = 0;
                string mouId = "";
                using (var conn = new System.Data.SqlClient.SqlConnection(connStr))
                {
                    conn.Open();
                    var cmd = conn.CreateCommand();
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.CommandText = "USP_ClaimMedicalScrutiny_Retrieve";
                    // FIX #1: SP signature is (@ClaimID bigint, @Slno tinyint) — only 2 params.
                    // Removed @IsFrmArchived = 0 which caused "too many arguments specified" error.
                    cmd.Parameters.AddWithValue("@ClaimID", claimIdLong2);
                    cmd.Parameters.AddWithValue("@Slno",    slNoInt);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (rdr.Read())
                        {
                            if (!rdr.IsDBNull(rdr.GetOrdinal("ProviderID")))
                                providerId = Convert.ToInt64(rdr["ProviderID"]);
                            if (!rdr.IsDBNull(rdr.GetOrdinal("MOUID")))
                                mouId = rdr["MOUID"].ToString();
                        }
                    }
                }

                if (providerId == 0)
                {
                    res.Success = false;
                    res.Message = "ProviderID not found for claimId=" + cId;
                    return Json(res, JsonRequestBehavior.AllowGet);
                }

                // Get all tariff files from S3 for this provider
                string s3Bucket  = System.Configuration.ConfigurationManager.AppSettings["ProviderDocbucketname"] ?? "prod-spectra-app-s3-provider-docs";
                string docPath   = System.Configuration.ConfigurationManager.AppSettings["ProviderTariffDocumentPath"] ?? "TariffDocs/";
                string webShare2 = System.Configuration.ConfigurationManager.AppSettings["ProviderTariffDocumentPathWebShare"] ?? "TariffDocs/";
                string accessKey = System.Configuration.ConfigurationManager.AppSettings["ProviderDocaccesskey"];
                string secretKey = System.Configuration.ConfigurationManager.AppSettings["ProviderDocsecretkey"];

                // List all tariff files for this provider.
                //
                // FIX #2/#3/#4: Replaced the call to Usp_TariffUploadDoc_FillDetails with a direct
                // query for three reasons:
                //   #2 — The SP signature is (@ProviderID int, @MOUID varchar(max),
                //        @TariffDocId int, @Type varchar(10)). The old code passed @Flag (wrong
                //        param name) and was missing @Type, causing "expects parameter @TariffDocId"
                //        error at runtime.
                //   #3 — The SP result set does NOT contain a FileType column in any branch. The old
                //        code's "if (!fileType.Contains(\"pdf\")) continue;" check would throw
                //        IndexOutOfRangeException. We now filter PDFs via Doc.FileName LIKE '%.pdf'
                //        directly in the WHERE clause.
                //   #4 — The SP result set does NOT contain an isOldDoc column in branches 2/3/4
                //        (only branch 1 returns it). The old code's "rdr[\"isOldDoc\"]" read would
                //        throw IndexOutOfRangeException. We now compute IsOldDoc inline using the
                //        same logic as branch 1 of the SP body: a doc is "old" if it has a mapping
                //        with mouid = 0.
                //
                // Behavior decisions worth flagging:
                //   (a) When mouId is non-empty: include both MOU-specific mappings AND mouid=0
                //       fallback mappings. SP branch 3 only returns MOU-specific. This change is
                //       intentional — without the fallback, claims whose providers only have
                //       generic (non-MOU-specific) tariffs would return no files, which is the
                //       documented complaint that motivated this refactor.
                //   (b) When mouId is empty: skip the MOU filter entirely (matches SP branch 4
                //       which returns all files for the provider).
                //   (c) GROUP BY on Doc.Id deduplicates rows when a document has multiple mappings
                //       (e.g. mapped to claim's MOU AND mouid=0). Otherwise the foreach loop below
                //       would download the same S3 object twice.
                var tariffFiles = new System.Collections.Generic.List<System.Tuple<string, DateTime, string, string>>();
                using (var conn = new System.Data.SqlClient.SqlConnection(connStr))
                {
                    conn.Open();
                    var cmd = conn.CreateCommand();
                    cmd.CommandType = System.Data.CommandType.Text;

                    // hasMou is false when mouId is empty OR "-1". This matches SP branch
                    // condition `@MOUID != '' and @MOUID != '-1'` — both values mean "no MOU
                    // specified" and trigger the unfiltered branch 4 of the original SP.
                    bool hasMou = !string.IsNullOrEmpty(mouId) && mouId != "-1";
                    string mouFilter = hasMou
                        ? "AND (Mp.MOUID = @MOUID OR Mp.MOUID = 0)"
                        : ""; // empty/-1 MOU → no filter (matches SP branch 4 behavior)

                    cmd.CommandText = @"
                        SELECT
                            Doc.SystemFileName,
                            Doc.FileName,
                            MAX(Doc.CreatedDateTime) AS UpdateDate,
                            MAX(CASE WHEN OMp.id IS NOT NULL THEN 'yes' ELSE 'no' END) AS IsOldDoc
                        FROM ProviderTariffDocs Doc WITH(NOLOCK)
                        INNER JOIN ProviderTariff_Map Mp WITH(NOLOCK) ON Doc.Id = Mp.DocumentId
                        LEFT JOIN (SELECT documentid, id FROM ProviderTariff_Map WITH(NOLOCK) WHERE MOUID = 0) OMp
                               ON Doc.Id = OMp.documentid
                        WHERE Mp.ProviderID = @ProviderID
                          AND Doc.Status = 1
                          AND Mp.Status  = 1
                          AND Doc.FileName LIKE '%.pdf'
                          " + mouFilter + @"
                        GROUP BY Doc.Id, Doc.SystemFileName, Doc.FileName
                        ORDER BY MAX(Doc.CreatedDateTime) DESC";

                    cmd.Parameters.AddWithValue("@ProviderID", providerId);
                    if (hasMou) cmd.Parameters.AddWithValue("@MOUID", mouId);

                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            string sysFileName = rdr["SystemFileName"]?.ToString();
                            if (string.IsNullOrWhiteSpace(sysFileName)) continue;

                            string isOldDoc = rdr["IsOldDoc"]?.ToString().ToLower() ?? "no";

                            DateTime updateDate = rdr.IsDBNull(rdr.GetOrdinal("UpdateDate"))
                                ? DateTime.MinValue
                                : Convert.ToDateTime(rdr["UpdateDate"]);

                            tariffFiles.Add(System.Tuple.Create(sysFileName, updateDate, isOldDoc, sysFileName));
                        }
                    }
                }

                if (tariffFiles.Count == 0)
                {
                    res.Success = false;
                    res.Message = "No tariff files found for providerId=" + providerId;
                    return Json(res, JsonRequestBehavior.AllowGet);
                }

                // Download all tariff PDFs from S3 and pick best by rules
                var s3Creds  = new Amazon.Runtime.BasicAWSCredentials(accessKey, secretKey);
                var s3Region = Amazon.RegionEndpoint.APSouth1;
                var s3TariffCandidates = new System.Collections.Generic.List<System.Tuple<string, DateTime, byte[]>>();

                using (var s3Client = new Amazon.S3.AmazonS3Client(s3Creds, s3Region))
                {
                    foreach (var tf in tariffFiles)
                    {
                        string sysName  = tf.Item1;
                        DateTime modDate = tf.Item2;
                        string isOldDoc = tf.Item3;
                        string s3Key    = isOldDoc == "yes"
                            ? webShare2 + sysName
                            : providerId.ToString() + "/" + docPath + sysName;
                        try
                        {
                            var request = new Amazon.S3.Model.GetPreSignedUrlRequest
                            {
                                BucketName = s3Bucket, Key = s3Key,
                                Expires    = DateTime.Now.AddMinutes(15)
                            };
                            string presignedUrl = s3Client.GetPreSignedURL(request);
                            using (var wc = new System.Net.WebClient())
                            {
                                byte[] pdfBytes = wc.DownloadData(presignedUrl);
                                s3TariffCandidates.Add(System.Tuple.Create(sysName, modDate, pdfBytes));
                            }
                        }
                        catch { /* skip failed */ }
                    }
                }

                if (s3TariffCandidates.Count == 0)
                {
                    res.Success = false;
                    res.Message = "Could not download any tariff files from S3 for providerId=" + providerId;
                    return Json(res, JsonRequestBehavior.AllowGet);
                }

                var bestTariffResult = PickTariffFileWithAI(s3TariffCandidates, isPsu, insurerCode);
                byte[] bestTariff = bestTariffResult?.Item2;
                string bestTariffName = bestTariffResult?.Item1 ?? (providerId + "-tariff.pdf");
                res.Success = true;
                res.Message = "Tariff loaded from S3. IsPSU=" + isPsu + " InsurerCode=" + insurerCode + " Files=" + s3TariffCandidates.Count
                            + " || TARIFF SELECTION: " + _lastTariffSelectionLog;
                res.Data    = new { fileName = bestTariffName, base64Content = Convert.ToBase64String(bestTariff) };
                var s3 = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                return Content(s3.Serialize(res), "application/json");
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog.GetDefault(null).Log(new Elmah.Error(ex));
                res.Success = false;
                res.Message = "Error loading tariff: " + ex.Message;
                return Json(res, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Compresses a PDF by re-writing it through iTextSharp PdfCopy with compression enabled.
        /// Reduces file size for large scanned PDFs before sending to Convex.
        /// </summary>
        private byte[] CompressPdf(byte[] pdfBytes)
        {
            try
            {
                using (var input  = new System.IO.MemoryStream(pdfBytes))
                using (var output = new System.IO.MemoryStream())
                {
                    var reader   = new iTextSharp.text.pdf.PdfReader(input);
                    // Reduce image quality to compress scanned PDFs
                    reader.RemoveUnusedObjects();
                    var document = new iTextSharp.text.Document();
                    var writer   = new iTextSharp.text.pdf.PdfCopy(document, output)
                    {
                        CompressionLevel = iTextSharp.text.pdf.PdfStream.BEST_COMPRESSION
                    };
                    writer.SetFullCompression();
                    document.Open();
                    for (int p = 1; p <= reader.NumberOfPages; p++)
                        writer.AddPage(writer.GetImportedPage(reader, p));
                    document.Close();
                    reader.Close();
                    byte[] compressed = output.ToArray();
                    // Only use compressed if it's actually smaller
                    return compressed.Length < pdfBytes.Length ? compressed : pdfBytes;
                }
            }
            catch { return pdfBytes; }
        }

        #endregion


        /// <summary>Helper: merge with explicit page cap.</summary>
        private byte[] MergePdfsWithCap(System.Collections.Generic.List<byte[]> pdfList, int maxPages)
        {
            try
            {
                using (var ms = new System.IO.MemoryStream())
                {
                    var document = new iTextSharp.text.Document();
                    var writer   = new iTextSharp.text.pdf.PdfCopy(document, ms);
                    document.Open();
                    var seenPageHashes = new System.Collections.Generic.HashSet<string>();
                    int totalPages = 0;
                    foreach (var pdfBytes in pdfList)
                    {
                        if (maxPages > 0 && totalPages >= maxPages) break;
                        var reader = new iTextSharp.text.pdf.PdfReader(pdfBytes);
                        for (int p = 1; p <= reader.NumberOfPages; p++)
                        {
                            if (maxPages > 0 && totalPages >= maxPages) break;
                            // MergePdfsWithCap: respects maxPages limit
                            byte[] pageBytes = reader.GetPageContent(p);
                            string pageHash;
                            using (var md5 = System.Security.Cryptography.MD5.Create())
                                pageHash = BitConverter.ToString(md5.ComputeHash(pageBytes ?? new byte[0])).Replace("-", "");
                            if (!seenPageHashes.Add(pageHash)) continue;
                            writer.AddPage(writer.GetImportedPage(reader, p));
                            totalPages++;
                        }
                        reader.Close();
                    }
                    document.Close();
                    return ms.ToArray();
                }
            }
            catch { return pdfList[0]; }
        }

        /// <summary>Helper: merge list of PDF byte arrays into one using iTextSharp.
        /// Deduplicates at page level using MD5 hash of each page content.</summary>
        private byte[] MergePdfs(System.Collections.Generic.List<byte[]> pdfList)
        {
            if (pdfList.Count == 1) return pdfList[0];
            try
            {
                using (var ms = new System.IO.MemoryStream())
                {
                    var document = new iTextSharp.text.Document();
                    var writer   = new iTextSharp.text.pdf.PdfCopy(document, ms);
                    document.Open();
                    var seenPageHashes = new System.Collections.Generic.HashSet<string>();
                    int totalPages = 0;
                    // No page cap — process all pages
                    foreach (var pdfBytes in pdfList)
                    {
                        var reader = new iTextSharp.text.pdf.PdfReader(pdfBytes);
                        for (int p = 1; p <= reader.NumberOfPages; p++)
                        {

                            // Hash raw page content stream to detect duplicate pages
                            byte[] pageBytes = reader.GetPageContent(p);
                            string pageHash;
                            using (var md5 = System.Security.Cryptography.MD5.Create())
                                pageHash = BitConverter.ToString(md5.ComputeHash(pageBytes ?? new byte[0])).Replace("-", "");

                            if (!seenPageHashes.Add(pageHash)) continue; // skip duplicate page
                            writer.AddPage(writer.GetImportedPage(reader, p));
                            totalPages++;
                        }
                        reader.Close();
                    }
                    document.Close();
                    return ms.ToArray();
                }
            }
            catch { return pdfList[0]; }
        }

        /// Called synchronously from JS before submitting to Convex, so it must be fast.
        ///
        /// Returns: { age, hospitalName, documentDate, dischargeDate }
        /// These are not in basicData JSON so cannot be read from the DOM directly.
        ///
        /// GET /MedicalScrutiny/GetClaimFieldsForValidation?claimId=xxx
        /// </summary>
        /// <summary>
        /// POST /MedicalScrutiny/SaveBillingForClaimAI
        /// Sets all service rows to zero except ServiceID=6 (Others).
        /// Others: BillAmount = hospitalBillAmt, Deduction = max(hospitalBillAmt - tariffAmt, 0).
        /// </summary>
        [HttpPost]
        public ActionResult SaveBillingForClaimAI(
            string claimId, string slNo,
            string hospitalBillAmount, string tariffAmount,
            string totalAmountApproved = null)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] == null)
                    return Json(new { success = false, message = "Session expired" });

                long claimIdLong; int slNoInt;
                if (!long.TryParse((claimId ?? "").Trim(), out claimIdLong) || claimIdLong <= 0)
                    return Json(new { success = false, message = "Invalid claimId" });
                if (!int.TryParse((slNo ?? "").Trim(), out slNoInt)) slNoInt = 1;

                decimal hospAmt = 0m, tariffAmt = 0m, approvedAmt = 0m;
                decimal.TryParse((hospitalBillAmount ?? "").Trim(), out hospAmt);
                decimal.TryParse((tariffAmount ?? "").Trim(), out tariffAmt);
                decimal.TryParse((totalAmountApproved ?? "").Trim(), out approvedAmt);

                // Bill Amount = Total Medical Bill (hospAmt)
                // Bill After Deductions = Total Amount Approved (min of bill, tariff, benefit plan limit)
                // Deductions = Bill Amount - Bill After Deductions
                decimal eligibleAmt  = approvedAmt > 0 ? approvedAmt
                                     : (tariffAmt > 0 ? Math.Min(hospAmt, tariffAmt) : hospAmt);
                decimal deductionAmt = hospAmt - eligibleAmt;
                if (deductionAmt < 0) deductionAmt = 0m;

                // ServiceDetails - all rows, only Others (6) has amounts
                int[] allServiceIds = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
                var dtServices = new System.Data.DataTable();
                dtServices.Columns.Add("ServiceID",        typeof(int));
                dtServices.Columns.Add("BillAmount",       typeof(decimal));
                dtServices.Columns.Add("DeductionAmount",  typeof(decimal));
                dtServices.Columns.Add("DiscountAmount",   typeof(decimal));
                dtServices.Columns.Add("EligibleAmount",   typeof(decimal));
                dtServices.Columns.Add("SanctionedAmount", typeof(decimal));
                dtServices.Columns.Add("BillRoomdays",     typeof(int));

                foreach (var svcId in allServiceIds)
                {
                    var row = dtServices.NewRow();
                    row["ServiceID"]       = svcId;
                    row["BillRoomdays"]    = 0;
                    row["DiscountAmount"]  = 0m;
                    if (svcId == 6)
                    {
                        row["BillAmount"]       = hospAmt;
                        row["DeductionAmount"]  = deductionAmt;
                        row["EligibleAmount"]   = eligibleAmt;
                        row["SanctionedAmount"] = eligibleAmt;
                    }
                    else
                    {
                        row["BillAmount"]       = 0m;
                        row["DeductionAmount"]  = 0m;
                        row["EligibleAmount"]   = 0m;
                        row["SanctionedAmount"] = 0m;
                    }
                    dtServices.Rows.Add(row);
                }

                // BillDetails - only Others row
                var dtBillDetails = new System.Data.DataTable();
                dtBillDetails.Columns.Add("ServiceID",       typeof(int));
                dtBillDetails.Columns.Add("BillSlNo",        typeof(byte));
                dtBillDetails.Columns.Add("BillNo",          typeof(string));
                dtBillDetails.Columns.Add("BillDate",        typeof(DateTime));
                dtBillDetails.Columns.Add("BillAmount",      typeof(decimal));
                dtBillDetails.Columns.Add("DeductionAmount", typeof(decimal));
                var billRow = dtBillDetails.NewRow();
                billRow["ServiceID"]       = 6;
                billRow["BillSlNo"]        = (byte)1;
                billRow["BillNo"]          = "AI";
                billRow["BillDate"]        = DateTime.Now.Date;
                billRow["BillAmount"]      = hospAmt;
                billRow["DeductionAmount"] = deductionAmt;
                dtBillDetails.Rows.Add(billRow);

                // DeductionDetails - only if deduction > 0, reason 3 = Restricted to agreed tariff
                System.Data.DataTable dtDeductions = null;
                if (deductionAmt > 0)
                {
                    dtDeductions = new System.Data.DataTable();
                    dtDeductions.Columns.Add("ServiceID",             typeof(int));
                    dtDeductions.Columns.Add("BillSlNo",              typeof(byte));
                    dtDeductions.Columns.Add("DeductionSlNo",         typeof(byte));
                    dtDeductions.Columns.Add("DeductionAmount",       typeof(decimal));
                    dtDeductions.Columns.Add("DeductionReasonID",     typeof(int));
                    dtDeductions.Columns.Add("IRDADeductionReasonID", typeof(int));
                    dtDeductions.Columns.Add("FreeTextValue",         typeof(string));
                    var dedRow = dtDeductions.NewRow();
                    dedRow["ServiceID"]             = 6;
                    dedRow["BillSlNo"]              = (byte)1;
                    dedRow["DeductionSlNo"]         = (byte)1;
                    dedRow["DeductionAmount"]       = deductionAmt;
                    dedRow["DeductionReasonID"]     = 3;  // Restricted to agreed tariff
                    dedRow["IRDADeductionReasonID"] = DBNull.Value;
                    dedRow["FreeTextValue"]         = "";
                    dtDeductions.Rows.Add(dedRow);
                }

                int userRegionId     = Convert.ToInt32(Session[SessionValue.UserRegionID]);
                int regionId         = Convert.ToInt32(Session[SessionValue.RegionID]);
                int roleId           = Session[SessionValue.UserRoleID] != null
                                       ? Convert.ToInt32(Session[SessionValue.UserRoleID])
                                       : 20;
                System.Data.DataTable dtDisc  = (System.Data.DataTable)(Session["ClaimDiscount"]);
                System.Data.DataTable dtTariffDisc = new System.Data.DataTable();
                dtTariffDisc.Columns.Add("ServiceID",      typeof(int));
                dtTariffDisc.Columns.Add("TariffAmount",   typeof(decimal));
                dtTariffDisc.Columns.Add("DiscountAmount", typeof(decimal));

                string vMessage = string.Empty;
                int result = _objClaimsVM.Save_ServiceBillingDetailsVM(
                    claimIdLong, (short)slNoInt,
                    dtBillDetails, dtDeductions, dtServices,
                    4, 18, roleId, regionId, userRegionId,
                    hospAmt.ToString(), eligibleAmt.ToString(),
                    eligibleAmt.ToString(), "0", "0", "",
                    tariffAmt.ToString(), "0",
                    dtDisc, dtTariffDisc,
                    0m, false, "", out vMessage);

                bool ok = result > 0 || string.IsNullOrEmpty(vMessage);
                return Json(new { success = ok, message = vMessage });
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog.GetDefault(null).Log(new Elmah.Error(ex));
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Called by ClaimAI iframe to save clinical details silently (no page refresh).
        /// Updates Claimsdetails: ProbableDiagnosis, ProbableLineOfTreatment,
        /// PresentComplaint, HospTreatmentTypeID.
        /// POST /MedicalScrutiny/SaveClinicalDetailsForClaimAI
        /// </summary>
        [HttpPost]
        public ActionResult SaveClinicalDetailsForClaimAI(
            string claimDetailsId,
            string probableDiagnosis,
            string probableLineOfTreatment,
            string presentComplaint,
            string processingRemarks,
            string doctorNotes,
            string hospTreatmentTypeId,
            string approvedFacilityId = null,
            string patientConditionId = null)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] == null)
                    return Json(new { success = false, message = "Session expired" });

                long numericId;
                if (!long.TryParse((claimDetailsId ?? "").Trim(), out numericId) || numericId <= 0)
                    return Json(new { success = false, message = "Invalid ClaimDetailsId: " + claimDetailsId });

                string connStr = System.Configuration.ConfigurationManager
                                       .ConnectionStrings["McarePlusEntities"]
                                       .ConnectionString;
                if (connStr.StartsWith("metadata=", StringComparison.OrdinalIgnoreCase))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(
                        connStr, @"provider connection string=""([^""]+)""",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (m.Success) connStr = m.Groups[1].Value.Replace("&quot;", "\"");
                }

                int rowsAffected = 0;
                string lastError = "";

                try
                {
                    using (var conn = new System.Data.SqlClient.SqlConnection(connStr))
                    {
                        conn.Open();

                        // Find the actual row ID — hdnClaimDetailsID may be the ID directly
                        // or we fall back to finding by ClaimID+SlNo
                        long rowId = numericId;
                        var checkCmd = conn.CreateCommand();
                        checkCmd.CommandText = "SELECT COUNT(1) FROM Claimsdetails WHERE ID = @ID AND ISNULL(Deleted,0) = 0";
                        checkCmd.Parameters.AddWithValue("@ID", rowId);
                        int exists = (int)checkCmd.ExecuteScalar();
        
                        if (exists == 0)
                        {
                            // hdnClaimDetailsID might actually be ClaimID — find latest SlNo row
                            var findCmd = conn.CreateCommand();
                            findCmd.CommandText = "SELECT TOP 1 ID FROM Claimsdetails WHERE ClaimID = @ClaimID AND ISNULL(Deleted,0) = 0 ORDER BY SlNo DESC";
                            findCmd.Parameters.AddWithValue("@ClaimID", rowId);
                            var found = findCmd.ExecuteScalar();
                            if (found != null && found != DBNull.Value)
                            {
                                rowId = Convert.ToInt64(found);
                                    }
                            else
                            {
                                return Json(new { success = false, rowsAffected = 0, error = "Row not found" });
                            }
                        }

                        var setClauses = new System.Collections.Generic.List<string>();
                        var cmd = conn.CreateCommand();

                        // Confirmed column names from INFORMATION_SCHEMA.COLUMNS
                        if (!string.IsNullOrWhiteSpace(probableDiagnosis))
                        {
                            setClauses.Add("Diagnosis = @Diagnosis");
                            cmd.Parameters.AddWithValue("@Diagnosis", probableDiagnosis.Trim());
                        }
                        if (!string.IsNullOrWhiteSpace(probableLineOfTreatment))
                        {
                            setClauses.Add("PlanOfTreatment = @PlanOfTreatment");
                            cmd.Parameters.AddWithValue("@PlanOfTreatment", probableLineOfTreatment.Trim());
                        }
                        if (!string.IsNullOrWhiteSpace(presentComplaint))
                        {
                            setClauses.Add("PresentComplaint = @PresentComplaint");
                            cmd.Parameters.AddWithValue("@PresentComplaint", presentComplaint.Trim());
                        }
                        if (!string.IsNullOrWhiteSpace(processingRemarks))
                        {
                            setClauses.Add("ExecutiveNotes = @ExecutiveNotes");
                            cmd.Parameters.AddWithValue("@ExecutiveNotes", processingRemarks.Trim());
                        }
                        if (!string.IsNullOrWhiteSpace(doctorNotes))
                        {
                            setClauses.Add("DoctorNotes = @DoctorNotes");
                            cmd.Parameters.AddWithValue("@DoctorNotes", doctorNotes.Trim());
                        }
                        if (!string.IsNullOrWhiteSpace(hospTreatmentTypeId))
                        {
                            int typeId;
                            if (int.TryParse(hospTreatmentTypeId.Trim(), out typeId) && typeId > 0)
                            {
                                setClauses.Add("TreatmentTypeID_P19 = @TreatmentTypeID");
                                cmd.Parameters.AddWithValue("@TreatmentTypeID", typeId);
                            }
                        }
                        if (!string.IsNullOrWhiteSpace(approvedFacilityId) && approvedFacilityId.Trim() != "0")
                        {
                            int facilityIdInt;
                            long facilityIdLong;
                            if (int.TryParse(approvedFacilityId.Trim(), out facilityIdInt) && facilityIdInt > 0)
                            {
                                setClauses.Add("ApprovedFacilityID = @ApprovedFacilityID");
                                cmd.Parameters.AddWithValue("@ApprovedFacilityID", facilityIdInt);
                            }
                            else if (long.TryParse(approvedFacilityId.Trim(), out facilityIdLong) && facilityIdLong > 0)
                            {
                                setClauses.Add("ApprovedFacilityID = @ApprovedFacilityID");
                                cmd.Parameters.AddWithValue("@ApprovedFacilityID", facilityIdLong);
                            }
                        }

                        // Always set PatientConditionID = 269 (Recovered) when saving from ClaimAI
                        // Can be overridden by passing a specific patientConditionId
                        int patientConditionIdInt = 269; // 269 = Recovered
                        if (!string.IsNullOrWhiteSpace(patientConditionId))
                            int.TryParse(patientConditionId.Trim(), out patientConditionIdInt);
                        setClauses.Add("MillimanConditionID = @PatientConditionID");
                        cmd.Parameters.AddWithValue("@PatientConditionID", patientConditionIdInt);

                        if (setClauses.Count == 0)
                            return Json(new { success = true, message = "Nothing to update" });

                        cmd.CommandText = string.Format(
                            "UPDATE Claimsdetails SET {0} WHERE ID = @ID AND ISNULL(Deleted,0) = 0",
                            string.Join(", ", setClauses));
                        cmd.Parameters.AddWithValue("@ID", rowId);

                        rowsAffected = cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    Elmah.ErrorLog.GetDefault(null).Log(new Elmah.Error(ex));
                    return Json(new { success = false, rowsAffected = 0, error = lastError });
                }

                return Json(new { success = rowsAffected > 0, rowsAffected = rowsAffected });
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog.GetDefault(null).Log(new Elmah.Error(ex));
                return Json(new { success = false, message = ex.Message + " | Line: " + (ex.StackTrace ?? "").Replace("\r\n", " -> ").Substring(0, Math.Min(300, (ex.StackTrace ?? "").Length)) });
            }
        }


        /// <summary>
        /// POST /MedicalScrutiny/SaveHospitalizationDetailsForClaimAI
        /// Saves ICUDays, RoomDays, BillNo to Claims table and ApprovedFacilityID to Claimsdetails.
        /// ICU=1, Room=0 for day-care cataract. BillNo defaults to "135" if empty.
        /// </summary>
        [HttpPost]
        public ActionResult SaveHospitalizationDetailsForClaimAI(
            string claimId          = null,
            string slNo             = null,
            string approvedFacilityId = null,
            string billNo           = null)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] == null)
                    return Json(new { success = false, message = "Session expired" });

                long claimIdLong;
                int  slNoInt;
                if (!long.TryParse((claimId ?? "").Trim(), out claimIdLong) || claimIdLong <= 0)
                    return Json(new { success = false, message = "Invalid ClaimID" });
                if (!int.TryParse((slNo ?? "1").Trim(), out slNoInt)) slNoInt = 1;

                // BillNo — use supplied value or default to "135"
                string billNoVal = (billNo ?? "").Trim();
                if (string.IsNullOrEmpty(billNoVal)) billNoVal = "135";

                string connStr = System.Configuration.ConfigurationManager
                                       .ConnectionStrings["McarePlusEntities"]
                                       .ConnectionString;
                if (connStr.StartsWith("metadata=", StringComparison.OrdinalIgnoreCase))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(
                        connStr, @"provider connection string=""([^""]+)""",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (m.Success) connStr = m.Groups[1].Value.Replace("&quot;", "\"");
                }

                int rowsAffected = 0;
                using (var conn = new System.Data.SqlClient.SqlConnection(connStr))
                {
                    conn.Open();

                    // 1. Update Claims table: BillNo, and ensure RoomDays/ICUDays are non-zero
                    //    (some validations block save when both are 0)
                    var cmdClaims = conn.CreateCommand();
                    cmdClaims.CommandText = @"
                        UPDATE Claims
                        SET    BillNo  = @BillNo,
                               RoomDays = CASE WHEN ISNULL(RoomDays, 0) = 0 AND ISNULL(ICUDays, 0) = 0 THEN 1 ELSE RoomDays END,
                               ICUDays  = CASE WHEN ISNULL(RoomDays, 0) = 0 AND ISNULL(ICUDays, 0) = 0 THEN 1 ELSE ICUDays  END
                        WHERE  ID = @ClaimID
                          AND  ISNULL(Deleted, 0) = 0";
                    cmdClaims.Parameters.AddWithValue("@BillNo",  billNoVal);
                    cmdClaims.Parameters.AddWithValue("@ClaimID", claimIdLong);
                    rowsAffected += cmdClaims.ExecuteNonQuery();

                    // 2. Update Claimsdetails: ApprovedFacilityID + IsFacilityChanged=1
                    // IsFacilityChanged is on Claimsdetails (not Claims).
                    // Setting it to 1 prevents Fill_HospitalizationDetails from resetting
                    // ddlApprovedFacility to 0 on page reload (the IsAprvFacilitychanged guard).
                    int facId;
                    if (!string.IsNullOrWhiteSpace(approvedFacilityId) &&
                        int.TryParse(approvedFacilityId.Trim(), out facId) && facId > 0)
                    {
                        var cmdDtl = conn.CreateCommand();
                        cmdDtl.CommandText = @"
                            UPDATE Claimsdetails
                            SET    ApprovedFacilityID = @FacID,
                                   IsFacilityChanged  = 1
                            WHERE  ClaimID = @ClaimID
                              AND  Slno    = @SlNo
                              AND  ISNULL(Deleted, 0) = 0";
                        cmdDtl.Parameters.AddWithValue("@FacID",   facId);
                        cmdDtl.Parameters.AddWithValue("@ClaimID", claimIdLong);
                        cmdDtl.Parameters.AddWithValue("@SlNo",    slNoInt);
                        rowsAffected += cmdDtl.ExecuteNonQuery();
                    }
                }

                // Debug: read back what was actually saved
                string debugApproved = "not read";
                string debugFacilityChanged = "not read";
                try {
                    using (var connCheck = new System.Data.SqlClient.SqlConnection(connStr))
                    {
                        connCheck.Open();
                        var chk = connCheck.CreateCommand();
                        chk.CommandText = "SELECT TOP 1 IsFacilityChanged, BillNo FROM Claims WHERE ID=@ID AND ISNULL(Deleted,0)=0";
                        chk.Parameters.AddWithValue("@ID", claimIdLong);
                        using (var r = chk.ExecuteReader()) {
                            if (r.Read()) {
                                debugFacilityChanged = r["IsFacilityChanged"]?.ToString() ?? "null";
                                debugApproved = r["BillNo"]?.ToString() ?? "null";
                            }
                        }
                        var chk2 = connCheck.CreateCommand();
                        chk2.CommandText = "SELECT TOP 1 ApprovedFacilityID FROM Claimsdetails WHERE ClaimID=@ID AND ISNULL(Deleted,0)=0 ORDER BY Slno DESC";
                        chk2.Parameters.AddWithValue("@ID", claimIdLong);
                        var approvedReadBack = chk2.ExecuteScalar();
                        debugApproved = "ApprovedFacilityID=" + (approvedReadBack?.ToString() ?? "null");
                    }
                } catch {}

                return Json(new { success = rowsAffected > 0, rowsAffected = rowsAffected, billNo = billNoVal,
                    debug_IsFacilityChanged = debugFacilityChanged,
                    debug_ApprovedFacilityID = debugApproved,
                    debug_claimId = claimIdLong, debug_slNo = slNoInt,
                    debug_approvedFacilityId_received = approvedFacilityId });
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog.GetDefault(null).Log(new Elmah.Error(ex));
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Called by ClaimAI to save a single coding row without knowing the exact DataTable schema.
        [HttpPost]
        // VERSION: 2026-04-27 — calls USP_CLA_SaveClaimCodingData with individual params
        public ActionResult SaveCodingRowForClaimAI(
            string claimId        = null,
            string slNo           = null,
            string tpaProcedureId = null,
            string icdCodeStr     = null,
            string eligibleAmount = null,
            string packageAmount  = null,
            string claimType      = null)   // Phase 6: disease type passed from JS; null = fall back to cataract default
        {
            try
            {
                if (Session[SessionValue.UserRegionID] == null)
                    return Json(new { success = false, message = "Session expired." });

                // Parse inputs
                long   claimIdLong  = 0;
                int    slNoInt      = 1;
                int    tpaProcId    = 0;
                decimal eligibleAmt = 0;
                decimal packageAmt  = 0;

                long.TryParse((claimId ?? "").Trim(),        out claimIdLong);
                int.TryParse((slNo ?? "1").Trim(),           out slNoInt);
                int.TryParse((tpaProcedureId ?? "").Trim(),  out tpaProcId);
                decimal.TryParse((eligibleAmount ?? "0").Trim(), out eligibleAmt);
                decimal.TryParse((packageAmount  ?? "0").Trim(), out packageAmt);

                if (claimIdLong == 0)
                    return Json(new { success = false, message = "Invalid ClaimID." });

                // Get connection string
                string connStr = System.Configuration.ConfigurationManager
                    .ConnectionStrings["McarePlusEntities"].ConnectionString;
                if (connStr.StartsWith("metadata=", StringComparison.OrdinalIgnoreCase))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(
                        connStr, "provider connection string=\"([^\"]+)\"",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (m.Success) connStr = m.Groups[1].Value.Replace("&quot;", "\"");
                }

                // IssueID is optional in SP (@IssueID tinyint=null) — skip lookup, pass null
                int issueId = 0;

                // Resolve ICD10 numeric ID from code string
                int icdNumericId = 0;
                if (!string.IsNullOrWhiteSpace(icdCodeStr))
                {
                    using (var conn = new System.Data.SqlClient.SqlConnection(connStr))
                    {
                        conn.Open();
                        string code = icdCodeStr.Trim();
                        while (code.Length > 0 && icdNumericId == 0)
                        {
                            var cmd = conn.CreateCommand();
                            cmd.CommandText = "SELECT TOP 1 ID FROM ICD10 WHERE DiseaseCode=@dc AND Deleted=0 ORDER BY Level DESC";
                            cmd.Parameters.AddWithValue("@dc", code);
                            var sc = cmd.ExecuteScalar();
                            if (sc != null && sc != DBNull.Value) { icdNumericId = Convert.ToInt32(sc); break; }
                            if (code.Contains("."))
                            {
                                int dot = code.LastIndexOf('.'); string after = code.Substring(dot + 1);
                                code = after.Length > 1
                                    ? code.Substring(0, dot + 1) + after.Substring(0, after.Length - 1)
                                    : code.Substring(0, dot);
                            }
                            else if (code.Length > 1) code = code.Substring(0, code.Length - 1);
                            else break;
                        }
                    }
                }

                // Call USP_CLA_SaveClaimCodingData with individual parameters
                // SP signature: @ClaimID, @Slno, @TPAProcedureID, @BillAmount, @PackageRate,
                //               @Discount, @EligibleAmount, @DisallowedAmount, @PayableAmount,
                //               @IssueID, @SpecialityType, @ICD10Code
                using (var conn = new System.Data.SqlClient.SqlConnection(connStr))
                {
                    conn.Open();
                    var cmd = conn.CreateCommand();
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.CommandText = "USP_CLA_SaveClaimCodingData";

                    cmd.Parameters.AddWithValue("@ClaimID",        claimIdLong);
                    cmd.Parameters.AddWithValue("@Slno",           (byte)slNoInt);
                    cmd.Parameters.AddWithValue("@TPAProcedureID", tpaProcId > 0 ? (object)tpaProcId : DBNull.Value);
                    // @BillAmount   = Total Medical Bill from iframe (no capping)
                    // @PackageRate  = NULL — package not used for coding row
                    // @EligibleAmount and @PayableAmount = eligible amount (after BP cap)
                    // @DisallowedAmount = bill - eligible (only if bill > eligible)
                    decimal disallowed = (packageAmt > 0 && eligibleAmt > 0 && packageAmt > eligibleAmt)
                                         ? (packageAmt - eligibleAmt) : 0m;

                    cmd.Parameters.AddWithValue("@BillAmount",     packageAmt > 0 ? (object)packageAmt : DBNull.Value);
                    cmd.Parameters.AddWithValue("@PackageRate",    DBNull.Value);
                    cmd.Parameters.AddWithValue("@Discount",       0m);
                    cmd.Parameters.AddWithValue("@EligibleAmount", eligibleAmt > 0 ? (object)eligibleAmt : DBNull.Value);
                    cmd.Parameters.AddWithValue("@DisallowedAmount", disallowed > 0 ? (object)disallowed : DBNull.Value);
                    cmd.Parameters.AddWithValue("@PayableAmount",  eligibleAmt > 0 ? (object)eligibleAmt : DBNull.Value);
                    cmd.Parameters.AddWithValue("@IssueID",        issueId > 0 ? (object)(byte)issueId : DBNull.Value);
                    cmd.Parameters.AddWithValue("@SpecialityType", DBNull.Value);
                    cmd.Parameters.AddWithValue("@ICD10Code",      icdNumericId > 0 ? (object)icdNumericId : DBNull.Value);

                    try
                    {
                        cmd.ExecuteNonQuery();
                    }
                    catch (System.Data.SqlClient.SqlException sqlEx) when (sqlEx.Message.IndexOf("BillingType_P51", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // SP doesn't compute BillingType for this case (e.g. maternity).
                        // Minimal INSERT using only columns we know exist from SP signature + BillingType_P51.
                        // All other columns will use their DB defaults.
                        conn.Close(); conn.Open();
                        var ins = conn.CreateCommand();
                        // Get UserRegionID from session
                        int userRegionId = 1;
                        if (Session[SessionValue.UserRegionID] != null)
                            int.TryParse(Session[SessionValue.UserRegionID].ToString(), out userRegionId);

                        // Step 1: Look up Level1/Level2/Level3/code from TPAProcedures self-referencing hierarchy.
                        //   l3 = selected procedure (e.g. 1331 "Normal delivery with well baby care")
                        //   l2 = parent (e.g. 402 "Normal Delivery")
                        //   l1 = grandparent (e.g. 401 "Obstetrics and Gynecology")
                        int    level1Id = 0, level2Id = 0, level3Id = tpaProcId;
                        string l3Name = null, pcsCode = null, pcsDesc = null;
                        int    treatmentType = 0;

                        if (tpaProcId > 0)
                        {
                            try
                            {
                                // First — check depth of the selected procedure ID.
                                // We need a true Level3 (leaf) row. If JS matched a Level2 (parent) ID,
                                // walk DOWN to the first child to get the real procedure.
                                int actualL3Id = tpaProcId;
                                int parentChain = 0;
                                int currentId   = tpaProcId;
                                int currentParent = 0;

                                // Determine if the selected ID is L1, L2, or L3 by counting ancestors
                                var depthCmd = conn.CreateCommand();
                                depthCmd.CommandText = "SELECT ISNULL(ParentId, 0) AS P FROM TPAProcedures WHERE ID = @id";
                                depthCmd.Parameters.AddWithValue("@id", currentId);
                                var pObj = depthCmd.ExecuteScalar();
                                currentParent = pObj != null && pObj != DBNull.Value ? Convert.ToInt32(pObj) : 0;

                                // Count ancestors: 0 ancestors = L1, 1 = L2, 2 = L3
                                int ancestorCount = 0;
                                int walkId = currentParent;
                                while (walkId > 0 && ancestorCount < 5)
                                {
                                    var wc = conn.CreateCommand();
                                    wc.CommandText = "SELECT ISNULL(ParentId, 0) FROM TPAProcedures WHERE ID = @id";
                                    wc.Parameters.AddWithValue("@id", walkId);
                                    var nextObj = wc.ExecuteScalar();
                                    int nextP = nextObj != null && nextObj != DBNull.Value ? Convert.ToInt32(nextObj) : 0;
                                    ancestorCount++;
                                    if (nextP == 0) break;
                                    walkId = nextP;
                                }

                                // If ancestorCount < 2, the matched ID is L1 or L2, not L3.
                                // Try to walk DOWN to find a leaf child.
                                if (ancestorCount < 2)
                                {
                                    var childCmd = conn.CreateCommand();
                                    childCmd.CommandText = @"SELECT TOP 1 ID FROM TPAProcedures WITH(NOLOCK)
                                                              WHERE ParentId = @parent AND ISNULL(Deleted, 0) = 0
                                                              ORDER BY ID";
                                    childCmd.Parameters.AddWithValue("@parent", tpaProcId);
                                    var childObj = childCmd.ExecuteScalar();
                                    if (childObj != null && childObj != DBNull.Value)
                                    {
                                        actualL3Id = Convert.ToInt32(childObj);
                                        System.Diagnostics.Debug.WriteLine(
                                            "[ClaimAI] Walked DOWN from L" + (ancestorCount + 1) +
                                            " ID=" + tpaProcId + " to leaf ID=" + actualL3Id);
                                    }
                                }

                                // Now look up Level1/Level2/Level3/PCS using the resolved L3 ID
                                var lookup = conn.CreateCommand();
                                lookup.CommandText = @"
                                    SELECT
                                        l3.ID                                       AS L3ID,
                                        ISNULL(l3.level3, ISNULL(l3.level2, l3.level1)) AS L3Name,
                                        ISNULL(l3.code, '')                         AS PCS,
                                        ISNULL(l3.TreatmentType_P19, 0)             AS TT,
                                        l2.ID                                       AS L2ID,
                                        l1.ID                                       AS L1ID
                                    FROM TPAProcedures l3 WITH(NOLOCK)
                                    LEFT JOIN TPAProcedures l2 WITH(NOLOCK) ON l2.ID = l3.ParentId
                                    LEFT JOIN TPAProcedures l1 WITH(NOLOCK) ON l1.ID = l2.ParentId
                                    WHERE l3.ID = @tpa";
                                lookup.Parameters.AddWithValue("@tpa", actualL3Id);
                                using (var rdr = lookup.ExecuteReader())
                                {
                                    if (rdr.Read())
                                    {
                                        level3Id      = Convert.ToInt32(rdr["L3ID"]);
                                        l3Name        = rdr["L3Name"]?.ToString();
                                        pcsCode       = rdr["PCS"]?.ToString();
                                        treatmentType = Convert.ToInt32(rdr["TT"]);
                                        level2Id      = rdr["L2ID"] != DBNull.Value ? Convert.ToInt32(rdr["L2ID"]) : 0;
                                        level1Id      = rdr["L1ID"] != DBNull.Value ? Convert.ToInt32(rdr["L1ID"]) : 0;
                                    }
                                }

                                // If PCS code is still empty, try grabbing it from the parent
                                // (some Level3 rows have empty code; the parent's code is often used)
                                if (string.IsNullOrEmpty(pcsCode) && level2Id > 0)
                                {
                                    var pcsCmd = conn.CreateCommand();
                                    pcsCmd.CommandText = "SELECT TOP 1 ISNULL(code, '') FROM TPAProcedures WHERE ID = @id";
                                    pcsCmd.Parameters.AddWithValue("@id", level2Id);
                                    var pcsObj = pcsCmd.ExecuteScalar();
                                    if (pcsObj != null && pcsObj != DBNull.Value)
                                        pcsCode = pcsObj.ToString();
                                }

                                pcsDesc = l3Name;
                                System.Diagnostics.Debug.WriteLine(
                                    "[ClaimAI] TPA lookup result: L1=" + level1Id +
                                    " L2=" + level2Id +
                                    " L3=" + level3Id +
                                    " (" + l3Name + ") PCS=" + pcsCode + " TT=" + treatmentType);
                            }
                            catch (Exception lookupEx)
                            {
                                System.Diagnostics.Debug.WriteLine("[ClaimAI] TPA lookup failed: " + lookupEx.Message);
                            }
                        }

                        if (treatmentType == 0) treatmentType = 66;  // Default to Surgical (cataract uses 66)

                        // ═══════ PHASE 6 — Fetch BillingType from ClaimAI rules registry ═══════
                        // Default to "cataract" if caller didn't pass claimType — preserves pre-Phase-6
                        // behavior (BillingType=201 hardcoded). The ClaimAIRulesClient helper class
                        // (defined at end of file) caches responses for 5 min and falls back to safe
                        // defaults on any error, so coding saves continue working during ClaimAI outages.
                        var resolvedClaimType  = string.IsNullOrWhiteSpace(claimType) ? "cataract" : claimType.Trim().ToLowerInvariant();
                        int  billingType       = ClaimAIRulesClient.GetBillingType(resolvedClaimType);
                        bool packageRateIsNull = ClaimAIRulesClient.GetPackageRateNull(resolvedClaimType);
                        System.Diagnostics.Debug.WriteLine(
                            "[ClaimAI] Coding row config for " + resolvedClaimType +
                            ": BillingType=" + billingType +
                            ", PackageRateNull=" + packageRateIsNull);
                        // ═══════ END PHASE 6 ADDITION ═══════

                        // Step 2: INSERT with full set of columns matching working cataract rows.
                        // Column names taken from cataract row sample: TPALevel1, TPALevel2, TPALevel3,
                        // PCSCode, PCSDescription, TreatementTypeID_19, Createddatetime, isGipsa,
                        // isDayCare, isPED, PackageRatio.
                        // Phase 6: BillingType_P51 and PackageRate now come from @bt and @pkgRate parameters
                        // sourced from the ClaimAI rules registry (was hardcoded 201 and NULL).
                        ins.CommandText = @"
                            INSERT INTO ClaimsCoding
                                (ClaimID, Slno, TPAProcedureID,
                                 TPALevel1, TPALevel2, TPALevel3,
                                 PCSCode, PCSDescription, TreatementTypeID_19,
                                 BillAmount, PackageRate, PackageRatio, Discount,
                                 EligibleAmount, DisallowedAmount, PayableAmount,
                                 ICDCode, BillingType_P51,
                                 isGipsa, isDayCare, isPED,
                                 Deleted, Createddatetime, CreatedUserRegionID)
                            OUTPUT INSERTED.ID
                            VALUES
                                (@cid, @slno, @tpa,
                                 @l1, @l2, @l3,
                                 @pcs, @pcsDesc, @tt,
                                 @bill, @pkgRate, 100.00, 0,
                                 @elig, @dis, @pay,
                                 @icd, @bt,
                                 0, 0, 0,
                                 0, GETDATE(), @region)";
                        ins.Parameters.AddWithValue("@cid",     claimIdLong);
                        ins.Parameters.AddWithValue("@slno",    (byte)slNoInt);
                        ins.Parameters.AddWithValue("@tpa",     tpaProcId > 0 ? (object)tpaProcId : DBNull.Value);
                        ins.Parameters.AddWithValue("@l1",      level1Id > 0 ? (object)level1Id : DBNull.Value);
                        ins.Parameters.AddWithValue("@l2",      level2Id > 0 ? (object)level2Id : DBNull.Value);
                        ins.Parameters.AddWithValue("@l3",      level3Id > 0 ? (object)level3Id : DBNull.Value);
                        ins.Parameters.AddWithValue("@pcs",     !string.IsNullOrEmpty(pcsCode) ? (object)pcsCode : DBNull.Value);
                        ins.Parameters.AddWithValue("@pcsDesc", !string.IsNullOrEmpty(pcsDesc) ? (object)pcsDesc : DBNull.Value);
                        ins.Parameters.AddWithValue("@tt",      treatmentType);
                        ins.Parameters.AddWithValue("@bill",    packageAmt > 0 ? (object)packageAmt : DBNull.Value);
                        ins.Parameters.AddWithValue("@elig",    eligibleAmt > 0 ? (object)eligibleAmt : DBNull.Value);
                        ins.Parameters.AddWithValue("@dis",     disallowed > 0 ? (object)disallowed : (object)0m);
                        ins.Parameters.AddWithValue("@pay",     eligibleAmt > 0 ? (object)eligibleAmt : DBNull.Value);
                        ins.Parameters.AddWithValue("@icd",     icdNumericId > 0 ? (object)icdNumericId : DBNull.Value);
                        ins.Parameters.AddWithValue("@region",  userRegionId);
                        // Phase 6: BillingType and PackageRate driven by claim type
                        ins.Parameters.AddWithValue("@bt",      billingType);
                        ins.Parameters.AddWithValue("@pkgRate", packageRateIsNull ? (object)DBNull.Value : (object)0m);

                        var newRowIdObj = ins.ExecuteScalar();
                        long newRowId   = newRowIdObj != null && newRowIdObj != DBNull.Value
                                          ? Convert.ToInt64(newRowIdObj) : 0;
                        System.Diagnostics.Debug.WriteLine("[ClaimAI] Inserted ClaimsCoding row ID=" + newRowId);
                    }
                }

                // Override ICDCode in ClaimsCoding with the exact doctor-selected code.
                // The SP links ICD from TPAProcedures (e.g. H25) which is less specific
                // than what the doctor selected (e.g. H25.11). Update directly after SP.
                if (icdNumericId > 0)
                {
                    using (var conn = new System.Data.SqlClient.SqlConnection(connStr))
                    {
                        conn.Open();
                        var upd = conn.CreateCommand();
                        upd.CommandText = @"UPDATE ClaimsCoding
                                            SET ICDCode = @icd
                                            WHERE ClaimID = @cid
                                              AND Slno    = @slno
                                              AND Deleted = 0
                                              AND CreatedDatetime = (
                                                  SELECT MAX(CreatedDatetime)
                                                  FROM ClaimsCoding WITH(NOLOCK)
                                                  WHERE ClaimID=@cid AND Slno=@slno AND Deleted=0
                                              )";
                        upd.Parameters.AddWithValue("@icd",  icdNumericId);
                        upd.Parameters.AddWithValue("@cid",  claimIdLong);
                        upd.Parameters.AddWithValue("@slno", (byte)slNoInt);
                        upd.ExecuteNonQuery();
                    }
                }

                return Json(new { success = true, message = "Coding saved." });
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog.GetDefault(null).Log(new Elmah.Error(ex));
                return Json(new { success = false, message = ex.Message });
            }
        }


        public ActionResult GetClaimFieldsForValidation(string claimId)
        {
            try
            {
                if (Session[SessionValue.UserRegionID] == null)
                    return Json(new { }, JsonRequestBehavior.AllowGet);

                if (string.IsNullOrWhiteSpace(claimId))
                    return Json(new { }, JsonRequestBehavior.AllowGet);

                long numericClaimId;
                if (!long.TryParse(claimId.Trim(), out numericClaimId))
                    return Json(new { }, JsonRequestBehavior.AllowGet);

                string connStr = System.Configuration.ConfigurationManager
                                       .ConnectionStrings["McarePlusEntities"]
                                       .ConnectionString;
                if (connStr.StartsWith("metadata=", StringComparison.OrdinalIgnoreCase))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(
                        connStr, @"provider connection string=""([^""]+)""",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (m.Success) connStr = m.Groups[1].Value.Replace("&quot;", "\"");
                }

                string age          = null;
                string hospitalName = null;
                string documentDate = null;
                string dischargeDate = null;

                using (var conn = new System.Data.SqlClient.SqlConnection(connStr))
                {
                    conn.Open();

                    // Age from MemberPolicy
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT TOP 1
                                CAST(mp.Age AS VARCHAR(10)) AS Age
                            FROM Claims c WITH (NOLOCK)
                            JOIN MemberPolicy mp WITH (NOLOCK)
                                ON mp.ID = c.MemberPolicyID
                            WHERE c.ID = @ClaimID
                              AND ISNULL(c.Deleted,0) = 0";
                        cmd.Parameters.AddWithValue("@ClaimID", numericClaimId);
                        using (var reader = cmd.ExecuteReader())
                            if (reader.Read())
                                age = reader["Age"] != DBNull.Value ? reader["Age"].ToString().Trim() : null;
                    }

                    // Hospital name from Mst_Provider via Claims
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT TOP 1 p.Name AS ProviderName
                            FROM Claims c WITH (NOLOCK)
                            JOIN Mst_Provider p WITH (NOLOCK) ON p.ID = c.ProviderID
                            WHERE c.ID = @ClaimID
                              AND ISNULL(c.Deleted,0) = 0";
                        cmd.Parameters.AddWithValue("@ClaimID", numericClaimId);
                        using (var reader = cmd.ExecuteReader())
                            if (reader.Read())
                                hospitalName = reader["ProviderName"] != DBNull.Value ? reader["ProviderName"].ToString().Trim() : null;
                    }

                    // Document date + discharge date — try Claimsdetails first, then Claimsdetail
                    foreach (var tbl in new[] { "Claimsdetails" })
                    {
                        try
                        {
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.CommandText = string.Format(@"
                                    SELECT TOP 1
                                        CONVERT(VARCHAR(10), dateofbill, 23)       AS DocumentDate,
                                        CONVERT(VARCHAR(10), dateofdischarge, 23)  AS DischargeDate
                                    FROM {0} WITH (NOLOCK)
                                    WHERE ClaimID = @ClaimID
                                      AND ISNULL(Deleted,0) = 0
                                    ORDER BY SlNo DESC", tbl);
                                cmd.Parameters.AddWithValue("@ClaimID", numericClaimId);
                                using (var reader = cmd.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        documentDate  = reader["DocumentDate"]  != DBNull.Value ? reader["DocumentDate"].ToString().Trim()  : null;
                                        dischargeDate = reader["DischargeDate"] != DBNull.Value ? reader["DischargeDate"].ToString().Trim()  : null;
                                        break;
                                    }
                                }
                            }
                        }
                        catch { /* try next table */ }
                    }
                }

                // Doctor Notes + ApprovedFacilityID + ReqFacilityID from Claimsdetails
                string doctorNotes = null;
                string approvedFacilityId = null;
                string approvedFacilityName = null;
                string patientConditionId = null;
                string reqFacilityId = null;
                string reqFacilityName = null;
                using (var conn2 = new System.Data.SqlClient.SqlConnection(connStr))
                {
                    conn2.Open();
                    using (var cmd = conn2.CreateCommand())
                    {
                        cmd.CommandText = @"SELECT TOP 1
                                                cd.DoctorNotes,
                                                cd.ApprovedFacilityID,
                                                cd.MillimanConditionID,
                                                cd.ReqFacilityID,
                                                fReq.Level2  AS ReqFacilityName,
                                                fAprv.Level2 AS ApprovedFacilityName
                                            FROM Claimsdetails cd WITH (NOLOCK)
                                            LEFT JOIN Mst_Facility fReq  WITH (NOLOCK) ON fReq.ID  = cd.ReqFacilityID
                                            LEFT JOIN Mst_Facility fAprv WITH (NOLOCK) ON fAprv.ID = cd.ApprovedFacilityID
                                            WHERE cd.ClaimID = @ClaimID AND ISNULL(cd.Deleted,0) = 0
                                            ORDER BY cd.SlNo DESC";
                        cmd.Parameters.AddWithValue("@ClaimID", numericClaimId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                if (reader["DoctorNotes"] != DBNull.Value)
                                    doctorNotes = reader["DoctorNotes"].ToString().Trim();
                                if (reader["ApprovedFacilityID"] != DBNull.Value)
                                    approvedFacilityId = reader["ApprovedFacilityID"].ToString().Trim();
                                if (reader["MillimanConditionID"] != DBNull.Value)
                                    patientConditionId = reader["MillimanConditionID"].ToString().Trim();
                                if (reader["ReqFacilityID"] != DBNull.Value)
                                    reqFacilityId = reader["ReqFacilityID"].ToString().Trim();
                                if (reader["ReqFacilityName"] != DBNull.Value)
                                    reqFacilityName = reader["ReqFacilityName"].ToString().Trim();
                                if (reader["ApprovedFacilityName"] != DBNull.Value)
                                    approvedFacilityName = reader["ApprovedFacilityName"].ToString().Trim();
                            }
                        }
                    }
                }

                return Json(new {
                    age                  = age,
                    hospitalName         = hospitalName,
                    documentDate         = documentDate,
                    dischargeDate        = dischargeDate,
                    doctorNotes          = doctorNotes,
                    approvedFacilityId   = approvedFacilityId,
                    approvedFacilityName = approvedFacilityName,
                    patientConditionId   = patientConditionId,
                    reqFacilityId        = reqFacilityId,
                    reqFacilityName      = reqFacilityName
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog.GetDefault(null).Log(new Elmah.Error(ex));
                return Json(new { }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// GET /MedicalScrutiny/IsClaimAISummaryAllowed?claimId=xxx&slNo=1
        /// Checks Claimsdetails directly — returns allowed=true only if
        /// ClaimTypeID=1 AND RequestTypeID=1.
        /// </summary>
        [HttpGet]
        public ActionResult IsClaimAISummaryAllowed(string claimId = null, string slNo = null)
        {
            try
            {
                long claimIdLong;
                int slNoInt;
                if (!long.TryParse((claimId ?? "").Trim(), out claimIdLong) || claimIdLong <= 0)
                    return Json(new { allowed = false, reason = "Invalid ClaimID" }, JsonRequestBehavior.AllowGet);
                if (!int.TryParse((slNo ?? "1").Trim(), out slNoInt)) slNoInt = 1;

                string connStr = System.Configuration.ConfigurationManager
                                       .ConnectionStrings["McarePlusEntities"].ConnectionString;
                if (connStr.StartsWith("metadata=", StringComparison.OrdinalIgnoreCase))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(
                        connStr, @"provider connection string=""([^""]+)""",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (m.Success) connStr = m.Groups[1].Value.Replace("&quot;", "\"");
                }

                int claimTypeId = 0, requestTypeId = 0;
                using (var conn = new System.Data.SqlClient.SqlConnection(connStr))
                {
                    conn.Open();
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        SELECT TOP 1 ClaimTypeID, RequestTypeID
                        FROM   Claimsdetails
                        WHERE  ClaimID = @ClaimID
                          AND  Slno    = @SlNo
                          AND  ISNULL(Deleted, 0) = 0";
                    cmd.Parameters.AddWithValue("@ClaimID", claimIdLong);
                    cmd.Parameters.AddWithValue("@SlNo",    slNoInt);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (rdr.Read())
                        {
                            claimTypeId   = rdr["ClaimTypeID"]   == DBNull.Value ? 0 : Convert.ToInt32(rdr["ClaimTypeID"]);
                            requestTypeId = rdr["RequestTypeID"] == DBNull.Value ? 0 : Convert.ToInt32(rdr["RequestTypeID"]);
                        }
                    }
                }

                bool allowed = (claimTypeId == 1 && requestTypeId == 1);
                return Json(new {
                    allowed       = allowed,
                    claimTypeId   = claimTypeId,
                    requestTypeId = requestTypeId,
                    reason        = allowed ? "OK" : "AI Summary is only available for Reimbursement Claim Type"
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { allowed = false, reason = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// POST /MedicalScrutiny/FinalizeClaimAISave
        /// Called after all ClaimAI saves complete. Sets BillingCorrection=2 and IsAprvFacilitychanged=1
        /// so that Claim Actions validation passes on next page load.
        /// </summary>
        [HttpPost]
        public ActionResult FinalizeClaimAISave(string claimId = null, string slNo = null)
        {
            try
            {
                long claimIdLong;
                int slNoInt;
                if (!long.TryParse((claimId ?? "").Trim(), out claimIdLong) || claimIdLong <= 0)
                    return Json(new { success = false, message = "Invalid ClaimID" });
                if (!int.TryParse((slNo ?? "1").Trim(), out slNoInt)) slNoInt = 1;

                string connStr = System.Configuration.ConfigurationManager
                                       .ConnectionStrings["McarePlusEntities"].ConnectionString;
                if (connStr.StartsWith("metadata=", StringComparison.OrdinalIgnoreCase))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(
                        connStr, @"provider connection string=""([^""]+)""",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (m.Success) connStr = m.Groups[1].Value.Replace("&quot;", "\"");
                }

                // DO NOT modify BillingCorrection or IsAprvFacilitychanged in DB here.
                // Spectra's native save buttons (Hospitalization, Coding, Bill) manage these
                // flags themselves based on user edits. Permanently overriding them caused
                // Claim Actions to disappear after manual edits + saves through Spectra.
                //
                // In-memory override (basicData[0].BillingCorrection = 2) is enough for the
                // current session — Spectra refresh / native save will compute the right
                // state from DB which is what we want.

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }


        public ActionResult GetBSIForClaimAI(string claimId)
        {
            // Allow ClaimAI's Next.js server to call this endpoint (URL from Web.config ClaimAIUrl)
            Response.Headers.Add("Access-Control-Allow-Origin", "*");
            Response.Headers.Add("Access-Control-Allow-Methods", "GET");

            var res = new ApiResponse<object>();
            try
            {
                if (Session[SessionValue.UserRegionID] == null)
                {
                    res.Success = false; res.ErrorCode = "ErrorCode#1";
                    res.Message = "Session expired.";
                    return Json(res, JsonRequestBehavior.AllowGet);
                }

                if (string.IsNullOrWhiteSpace(claimId))
                {
                    res.Success = false;
                    res.Message = "claimId is required.";
                    return Json(res, JsonRequestBehavior.AllowGet);
                }

                // Step 1 — resolve MemberPolicyID and SITypeID from claimId
                string connStr = System.Configuration.ConfigurationManager
                                       .ConnectionStrings["McarePlusEntities"]
                                       .ConnectionString;
                if (connStr.StartsWith("metadata=", StringComparison.OrdinalIgnoreCase))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(
                        connStr, @"provider connection string=""([^""]+)""",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (m.Success) connStr = m.Groups[1].Value.Replace("&quot;", "\"");
                }

                long   memberPolicyId = 0;
                int    siTypeId       = 6; // default: individual
                byte   slNo          = 1;

                using (var conn = new System.Data.SqlClient.SqlConnection(connStr))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT TOP 1
                                c.MemberPolicyID,
                                ISNULL(ms.SITypeID, 6) AS SITypeID,
                                ISNULL(c.SlNo, 1)      AS SlNo
                            FROM Claims c WITH (NOLOCK)
                            LEFT JOIN MemberSI ms WITH (NOLOCK)
                                ON ms.MemberPolicyID = c.MemberPolicyID
                                AND ISNULL(ms.Deleted, 0) = 0
                            WHERE CAST(c.ID AS VARCHAR(50)) = @ClaimID
                              AND ISNULL(c.Deleted, 0) = 0
                            ORDER BY ms.ID DESC";
                        cmd.Parameters.AddWithValue("@ClaimID", claimId.Trim());
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                memberPolicyId = reader["MemberPolicyID"] != DBNull.Value
                                    ? Convert.ToInt64(reader["MemberPolicyID"]) : 0;
                                siTypeId = reader["SITypeID"] != DBNull.Value
                                    ? Convert.ToInt32(reader["SITypeID"]) : 6;
                                slNo = reader["SlNo"] != DBNull.Value
                                    ? Convert.ToByte(reader["SlNo"]) : (byte)1;
                            }
                        }
                    }
                }

                if (memberPolicyId == 0)
                {
                    res.Success = false;
                    res.Message = "Claim not found: " + claimId;
                    return Json(res, JsonRequestBehavior.AllowGet);
                }

                // Step 2 — call SpectraUtils DLL GetBSI()
                var claimIdLong = Convert.ToInt64(claimId.Trim());
                BSIinfo objBSI  = new SpectraUtils.Main().GetBSI(
                    memberPolicyId, siTypeId, claimIdLong, slNo);

                // Step 3 — return as JSON (MaxJsonLength for large utilisation arrays)
                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer
                {
                    MaxJsonLength = int.MaxValue
                };
                return Content(serializer.Serialize(objBSI), "application/json");
            }
            catch (Exception ex)
            {
                Elmah.ErrorLog.GetDefault(null).Log(new Elmah.Error(ex));
                res.Success = false;
                res.Message = "Error fetching BSI: " + ex.Message;
                return Json(res, JsonRequestBehavior.AllowGet);
            }
        }


        #region ClaimAI Metrics

        /// <summary>
        /// Logs a ClaimAI event (save click or field change) into ClaimAI_EventLog
        /// and increments the save count in ClaimAI_SaveCount.
        /// Called from Index.cshtml via fire-and-forget AJAX on every save click.
        /// </summary>
        [HttpPost]
        public ActionResult LogClaimAIEvent(string claimId, string slNo,
            string eventType, string fieldName = null,
            string aiValue = null, string userValue = null,
            string claimType = null)
        {
            try
            {
                var userID   = Session[SessionValue.LoginUserID]   != null
                    ? Convert.ToInt64(Session[SessionValue.LoginUserID])
                    : (long?)null;
                var userName = Session[SessionValue.LoginUserName] != null
                    ? Session[SessionValue.LoginUserName].ToString()
                    : null;

                var ipAddress = Request.UserHostAddress;
                if (Request.ServerVariables["HTTP_X_FORWARDED_FOR"] != null)
                    ipAddress = Request.ServerVariables["HTTP_X_FORWARDED_FOR"].Split(',')[0].Trim();

                var claimIdLong = Convert.ToInt64(claimId);
                var slNoInt     = string.IsNullOrEmpty(slNo) ? 1 : Convert.ToInt32(slNo);

                string _connStr = System.Configuration.ConfigurationManager
                                        .ConnectionStrings["McarePlusEntities"].ConnectionString;
                // McarePlusEntities is an EF connection string — extract inner SQL connection string
                if (_connStr.StartsWith("metadata=", StringComparison.OrdinalIgnoreCase))
                {
                    var _m = System.Text.RegularExpressions.Regex.Match(
                        _connStr, @"provider connection string=""([^""]+)""",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (_m.Success) _connStr = _m.Groups[1].Value.Replace("&quot;", "\"");
                }

                using (var conn = new System.Data.SqlClient.SqlConnection(_connStr))
                {
                    conn.Open();

                    // 1. Insert detailed event log
                    using (var cmd = new System.Data.SqlClient.SqlCommand("USP_ClaimAI_LogEvent", conn))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@ClaimID",   claimIdLong);
                        cmd.Parameters.AddWithValue("@SlNo",      slNoInt);
                        cmd.Parameters.AddWithValue("@EventType", eventType ?? "SAVE_CLICK");
                        cmd.Parameters.AddWithValue("@FieldName", (object)fieldName ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@AIValue",   (object)aiValue   ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@UserValue", (object)userValue  ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@ClaimType", (object)claimType  ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@UserID",    (object)userID     ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@UserName",  (object)userName   ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@IPAddress", (object)ipAddress  ?? DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }

                    // 2. Increment save count only for SAVE_CLICK
                    if (string.Equals(eventType, "SAVE_CLICK", StringComparison.OrdinalIgnoreCase))
                    {
                        using (var cmd2 = new System.Data.SqlClient.SqlCommand("USP_ClaimAI_IncrementSaveCount", conn))
                        {
                            cmd2.CommandType = System.Data.CommandType.StoredProcedure;
                            cmd2.Parameters.AddWithValue("@ClaimID",   claimIdLong);
                            cmd2.Parameters.AddWithValue("@SlNo",      slNoInt);
                            cmd2.Parameters.AddWithValue("@ClaimType", (object)claimType ?? DBNull.Value);
                            cmd2.Parameters.AddWithValue("@UserName",  (object)userName  ?? DBNull.Value);
                            cmd2.ExecuteNonQuery();
                        }
                    }
                }

                return Json(new { success = true }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[ClaimAI Metrics] LogClaimAIEvent error: " + ex.Message);
                return Json(new { success = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        #endregion ClaimAI Metrics

    }

    /// <summary>
    /// PHASE 6 — Lightweight client that fetches disease-rules from ClaimAI's
    /// /api/rules/[claimType] endpoint and caches the result in memory.
    ///
    /// Used by SaveCodingRowForClaimAI to get the right BillingType_P51 value
    /// per disease (201 for cataract, 202 for maternity, etc.) instead of the
    /// previously hardcoded literal.
    ///
    /// Cache TTL: 5 minutes per claim type. Falls back to safe hardcoded
    /// defaults if the fetch fails — coding saves continue working during
    /// a ClaimAI outage.
    /// </summary>
    internal static class ClaimAIRulesClient
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, CachedRules> _cache
            = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedRules>();
        private static readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Get the BillingType_P51 value for a claim type.
        /// Falls back to 201 (cataract default) on any error — matches pre-Phase-6 hardcoded value.
        /// </summary>
        public static int GetBillingType(string claimType)
        {
            try
            {
                var rules = GetRulesCached(claimType);
                return rules != null && rules.codingRow != null ? rules.codingRow.billingType : 201;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[ClaimAIRulesClient] GetBillingType failed for " +
                    (claimType ?? "(null)") + ": " + ex.Message);
                return 201;
            }
        }

        /// <summary>
        /// Get whether PackageRate should be NULL for this claim type.
        /// True for maternity (bill only), false for cataract (bill + package).
        /// </summary>
        public static bool GetPackageRateNull(string claimType)
        {
            try
            {
                var rules = GetRulesCached(claimType);
                return rules != null && rules.codingRow != null && rules.codingRow.packageRateNull;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get the treatment type (TreatementTypeID_19). Defaults to 66 (Surgical).
        /// </summary>
        public static int GetTreatmentType(string claimType)
        {
            try
            {
                var rules = GetRulesCached(claimType);
                return rules != null && rules.codingRow != null ? rules.codingRow.treatmentType : 66;
            }
            catch
            {
                return 66;
            }
        }

        /// <summary>
        /// Get the default facility ID for the Approved Accommodation dropdown.
        /// Returns null if the disease has no default (e.g. maternity).
        /// </summary>
        public static int? GetDefaultFacilityId(string claimType)
        {
            try
            {
                var rules = GetRulesCached(claimType);
                return rules != null && rules.codingRow != null ? rules.codingRow.defaultFacilityId : null;
            }
            catch
            {
                return null;
            }
        }

        // ───────────────────────────────────────────────────────────────
        // Internal — cache lookup with TTL
        // ───────────────────────────────────────────────────────────────
        private static RulesPayload GetRulesCached(string claimType)
        {
            var key = string.IsNullOrWhiteSpace(claimType) ? "other" : claimType.Trim().ToLowerInvariant();

            CachedRules cached;
            if (_cache.TryGetValue(key, out cached) && cached.FetchedAt + _cacheTtl > DateTime.UtcNow)
            {
                return cached.Rules;
            }

            // Cache miss or expired — fetch fresh
            var fresh = FetchRules(key);
            if (fresh != null)
            {
                _cache[key] = new CachedRules { Rules = fresh, FetchedAt = DateTime.UtcNow };
            }
            return fresh;
        }

        private static RulesPayload FetchRules(string claimType)
        {
            // Read base URL from Web.config — same key used elsewhere in MedicalScrutinyController
            var baseUrl = System.Configuration.ConfigurationManager.AppSettings["ClaimAIUrl"];
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                System.Diagnostics.Debug.WriteLine("[ClaimAIRulesClient] ClaimAIUrl not configured in Web.config.");
                return null;
            }

            // ─── Production HTTPS setup ─────────────────────────────────────
            // Mirror the EXACT pattern used by every other HTTPS call in this
            // controller (see lines 6161, 6261, 6308, 7451, 7686 for reference).
            // Without these two settings, the call works on localhost but fails
            // in production with "Could not establish trust relationship for the
            // SSL/TLS secure channel" or "TLS handshake failed".
            //
            // - SecurityProtocol: enable TLS 1.2 + 1.1 + 1.0 so any TLS version
            //   the network path can negotiate works (some intercepting proxies
            //   downgrade from 1.2).
            // - ServerCertificateValidationCallback: accept all certs. The
            //   ClaimAI EC2 may use a self-signed or non-public CA cert, and
            //   the rest of this controller already bypasses validation —
            //   we follow the same pattern for consistency.
            System.Net.ServicePointManager.SecurityProtocol =
                System.Net.SecurityProtocolType.Tls12 |
                System.Net.SecurityProtocolType.Tls11 |
                System.Net.SecurityProtocolType.Tls;
            System.Net.ServicePointManager.ServerCertificateValidationCallback =
                (sender, cert, chain, errors) => true;
            // ────────────────────────────────────────────────────────────────

            var url = baseUrl.TrimEnd('/') + "/api/rules/" + Uri.EscapeDataString(claimType);

            var request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
            request.Method  = "GET";
            request.Timeout = 5000;  // 5 sec — don't block save flow on slow ClaimAI
            request.Accept  = "application/json";
            request.UserAgent = "Spectra-ClaimAIRulesClient/1.0";  // helps identify in ClaimAI logs

            try
            {
                using (var response = (System.Net.HttpWebResponse)request.GetResponse())
                using (var stream   = response.GetResponseStream())
                using (var reader   = new System.IO.StreamReader(stream))
                {
                    var json = reader.ReadToEnd();
                    var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                    var parsed = serializer.Deserialize<RulesPayload>(json);
                    return parsed;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[ClaimAIRulesClient] Fetch failed for " + claimType + ": " + ex.Message);
                return null;
            }
        }

        // ───────────────────────────────────────────────────────────────
        // DTO shapes — only the fields we consume in C#
        // ───────────────────────────────────────────────────────────────
        private class CachedRules
        {
            public RulesPayload Rules { get; set; }
            public DateTime FetchedAt { get; set; }
        }

        public class RulesPayload
        {
            public string type  { get; set; }
            public string label { get; set; }
            public CodingRowPayload codingRow { get; set; }
        }

        public class CodingRowPayload
        {
            public int  billingType       { get; set; }
            public int  treatmentType     { get; set; }
            public decimal packageRatio   { get; set; }
            public int? defaultFacilityId { get; set; }
            public bool packageRateNull   { get; set; }
        }
    }
}
