﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DanGame.Models;
using System.Linq;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Web;
using System.Security.Cryptography;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Collections;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static System.Net.WebRequestMethods;


public class AuthorizeUserAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var session = context.HttpContext.Session;
        var userIdStr = session.GetString("UserId");

        if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out _))
        {
            context.Result = new UnauthorizedResult();
        }
        base.OnActionExecuting(context);
    }
}



namespace DanGame.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class ShoppingCartController : Controller
    {
        private DanGameContext context;
        [HttpGet("apps")]
        public List<App> Apps()
        {
            var query = from o in context.Apps
                        select o;
            List<App> result = query.Take(10).ToList();
            return result;
        }

        [HttpPost("getcreditinfos")]
        public IActionResult getcreditinfos()

        {
            int userId = Convert.ToInt32(Request.HttpContext.Session.GetString("UserId"));
            var query = from o in context.CreditCardInfos
                        where o.UserId == userId
                        select o;

            var result = query.ToList();

            return result == null ? NotFound() : Ok(result);
        }


        [HttpGet("User/ShoppingCart")]
        [AuthorizeUser]
        async public Task<ShoppingCart[]> GetUserShoppingItem()
        {
            int userId = Convert.ToInt32(Request.HttpContext.Session.GetString("UserId"));

            var query = from shoppingCart in context.ShoppingCarts
                        where shoppingCart.UserId == userId
                        select shoppingCart;

            return await query.ToArrayAsync();
        }













        //POST: /CreditCardInfo
        [HttpPost("PostCreditCardInfo")]
        [AuthorizeUser]
        public async Task<ActionResult<CreditCardInfo>> PostCreditCardInfo([FromBody] CreditCardInfo creditCardInfo, [FromQuery] bool save) //這邊帶兩個參數，透過 save: $("#gridCheck").is(":checked")傳進來是bool來判斷
        {
            //前端送進來的creditCardInfo，是沒有設定userId的，所以這邊要透過認證跟seeion拿Id，確定這個信用卡資訊是哪一個用戶得
            int userId = Convert.ToInt32(Request.HttpContext.Session.GetString("UserId"));
            creditCardInfo.UserId = userId;
            // 檢查是否已經存在相同的信用卡信息
            var existingCard = await context.CreditCardInfos
                .FirstOrDefaultAsync(c => c.UserId == creditCardInfo.UserId &&
                                          c.PaymentMethod == creditCardInfo.PaymentMethod &&
                                          c.CardNumber == creditCardInfo.CardNumber &&
                                          c.ExpiryMonth == creditCardInfo.ExpiryMonth &&
                                          c.ExpiryYear == creditCardInfo.ExpiryYear &&
                                          c.FirstName == creditCardInfo.FirstName &&
                                          c.LastName == creditCardInfo.LastName &&
                                          c.City == creditCardInfo.City &&
                                          c.BillingAddress == creditCardInfo.BillingAddress &&
                                          c.PostalCode == creditCardInfo.PostalCode &&
                                          c.BillingAddress2 == creditCardInfo.BillingAddress2 &&
                                          c.PhoneNumber == creditCardInfo.PhoneNumber);

            if (existingCard != null)
            {
                // 如果已經存在，返回一個合適的響應，例如 409 Conflict 狀態碼
                return Conflict(new { message = "Credit card info already exists." });
            }




            if (save)
            {
                context.CreditCardInfos.Add(creditCardInfo);
                await context.SaveChangesAsync();
            }
            //$"creditCardInfo-{creditCardInfo.UserId}那這個key就會知道{creditCardInfo.UserId}是根據上面驗證後的useid
            Request.HttpContext.Session.SetString($"creditCardInfo-{creditCardInfo.UserId}", JsonConvert.SerializeObject(creditCardInfo));
          
            

            //return CreatedAtAction("GetCreditCardInfo", new { id = creditCardInfo.UserId }, creditCardInfo);


            return Ok(Request.HttpContext.Session.GetString($"creditCardInfo-{creditCardInfo.UserId}"));

        }

        //測試取得seeion
        [HttpGet("miketest")]
        public ActionResult test()
        {
            return Ok(Request.HttpContext.Session.GetString($"creditCardInfo-2"));
        }

        [HttpGet("index")]
        public IActionResult ShoppingCartIndex()
        {
            return View();
        }

        [HttpGet("CreditCardInfo")]
        public IActionResult CreditCardInfo()
        {
            return View();
        }

        [HttpGet("Gotocheck")]
        [AuthorizeUser]
        public IActionResult Gotocheck()
        {
            var UserId = Request.HttpContext.Session.GetString("UserId");
            var query = from o in context.ShoppingCarts
                        where o.UserId == Convert.ToInt32(UserId)
                        select new
                        {
                            Price=o.App.AppDetail.Price,
                            AppName= o.App.AppName 

                        };
            //var total = query.Sum()
            var total = query.Select( (o) => o.Price).Sum();
            var AppName = string.Join("#", query.Select((o) => o.AppName).ToArray());
            

            Dictionary<string, string> parameters = new Dictionary<string, string>
            {
                { "ChoosePayment", "ALL" },
                { "EncryptType", "1" },
                { "ItemName", AppName },
                { "MerchantID", "3002607" },
                { "MerchantTradeDate", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") },
                { "MerchantTradeNo", $"Mike{DateTime.Now.ToString("yyyyMMddHHmmss")}" },
                { "PaymentType", "aio" },
                { "ReturnURL", "http://localhost:5000/ShoppingCart/Ecplay"},/*http://example.com/ShoppingCart/Ecplay*/
                { "TotalAmount", $"{total}" },
                { "TradeDesc", "遊戲" },
            };

            parameters.Add("CheckMacValue", BuildCheckMacValue(parameters));

            return View(parameters);
        }


        private string BuildCheckMacValue(Dictionary<string, string> parameters)
        {
            List<string> parametersString = new List<string>();
            foreach (var parameter in parameters)
            {
                parametersString.Add($"{parameter.Key}={parameter.Value}");
            }
            //這邊是物件所以印出來System.Collections.Generic.List`1[System.String]長這樣，所以我下面把他轉成字串才可以看到結構
            Console.WriteLine(parametersString);
            Console.WriteLine(JsonConvert.SerializeObject(parametersString)); //這邊是把物件轉json，用JsonConvert可以物件轉json，也可以json轉物件
            Console.WriteLine(string.Join(", ", parametersString));

            string szCheckMacValue;

            // 產生檢查碼
            szCheckMacValue = string.Format("HashKey={0}&{1}&HashIV={2}", "pwFHCqoQZGmho4w6", string.Join("&", parametersString), "EkRm7iFT261dpevs");
            szCheckMacValue = HttpUtility.UrlEncode(szCheckMacValue).ToLower();

            using (SHA256 mySHA256 = SHA256.Create())
            {
                szCheckMacValue = BitConverter.ToString(mySHA256.ComputeHash(Encoding.UTF8.GetBytes(szCheckMacValue))).Replace("-", "");
            }
            Console.WriteLine(szCheckMacValue.ToUpper());
            return szCheckMacValue.ToUpper();
        }



        //private string BuildCheckMacValue(string parameters, int encryptType = 0)
        //{
        //    string szCheckMacValue = String.Empty;
        //    // 產生檢查碼。
        //    szCheckMacValue = String.Format("HashKey={0}{1}&HashIV={2}", this.HashKey, parameters, this.HashIV);
        //    szCheckMacValue = HttpUtility.UrlEncode(szCheckMacValue).ToLower();
        //    if (encryptType == 1)
        //    {
        //        szCheckMacValue = SHA256Encoder.Encrypt(szCheckMacValue);
        //    }
        //    else
        //    {
        //        szCheckMacValue = MD5Encoder.Encrypt(szCheckMacValue);
        //    }

        //    return szCheckMacValue;
        //}















        [HttpPost("Ecplay")]
        public IActionResult Ecplay()
        {
           
            // 获取ECPay返回的参数
            var merchantID = Request.Form["MerchantID"];
            var merchantTradeNo = Request.Form["MerchantTradeNo"];
            var paymentDate = Request.Form["PaymentDate"];
            var paymentType = Request.Form["PaymentType"];
            var tradeAmt = Request.Form["TradeAmt"];
            var tradeNo = Request.Form["TradeNo"];
            var rtnCode = Request.Form["RtnCode"];
            var rtnMsg = Request.Form["RtnMsg"];

            // 验证CheckMacValue
            var checkMacValue = Request.Form["CheckMacValue"];
            var parameters = new Dictionary<string, string>
    {
        { "MerchantID", merchantID },
        { "MerchantTradeNo", merchantTradeNo },
        { "PaymentDate", paymentDate },
        { "PaymentType", paymentType },
        { "TradeAmt", tradeAmt },
        { "TradeNo", tradeNo },
        { "RtnCode", rtnCode },
        { "RtnMsg", rtnMsg }
    };
            var calculatedCheckMacValue = BuildCheckMacValue(parameters);
            if (calculatedCheckMacValue != checkMacValue)
            {
                // 验证失败，处理错误
                return Content("CheckMacValue验证失败");
            }

            if (rtnCode == "1")
            {
                // 支付成功
                ViewBag.PaymentSuccess = true;
            }
            else
            {
                // 支付失败
                ViewBag.PaymentSuccess = false;
            }
            return View();
        }


        [HttpGet("getuserShoppingCart")]
        public ActionResult getuserShoppingCart()
        {
            int UserID = Convert.ToInt32(Request.HttpContext.Session.GetString("UserId"));
            var query = from o in context.ShoppingCarts
                        where o.UserId == UserID
                        select o;
            var result = query.ToList();

            return result == null ? NotFound() : Ok(result);
        }


        // API: POST API/App/AppsDetail 第一頁面取得 App 的詳細資料 
        [HttpPost("App/Detail")]
        public async Task<AppDetail[]> AppsDetail([FromBody] params int[] appIds)
        {
            var query = from app in context.AppDetails
                        where appIds.Contains(app.AppId)
                        select app;
            return await query.ToArrayAsync();
        }


        //第三頁結帳頁面取得app資料
        [HttpPost("App/Detail/check")]
        public async Task<AppDetail[]> AppsDetailCheck([FromBody] params int[] appIds)
        {
            var query = from app in context.AppDetails
                        where appIds.Contains(app.AppId)
                        select app;
            return await query.ToArrayAsync();
        }


        //第三頁結帳取得信用卡資料
        [HttpGet("GetCreditCardInfo/check")]
        [AuthorizeUser]
        public IActionResult GetCreditCardInfo()
        {
           
            int userId = Convert.ToInt32(Request.HttpContext.Session.GetString("UserId"));
            string sessionKey = $"creditCardInfo-{userId}";

            // 從 Session 中取得資料
            var creditCardInfoJson = HttpContext.Session.GetString(sessionKey);

            if (!string.IsNullOrEmpty(creditCardInfoJson))
            {
                // 如果找到了資料，則將 JSON 字串轉換為 CreditCardInfo 物件
                var creditCardInfo = JsonConvert.DeserializeObject<CreditCardInfo>(creditCardInfoJson);
                var query = from o in context.Users
                            where o.UserId == userId
                            select new {
                                CreditCardInfo = creditCardInfo,
                                user =o.UserName,
                            };
                
                return Ok(query.FirstOrDefault());
            }
            else
            {
                // 如果找不到資料，返回 404 錯誤
                return NotFound("No credit card info found for this user.");
            }
        }





















        public ShoppingCartController(DanGameContext dbContext)
        {
            context = dbContext;
        }



    }
}