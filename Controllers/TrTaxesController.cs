using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TaxForm.Models;

using iText.Kernel.Pdf.Canvas.Parser.Listener;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Text;
//using Microsoft.AspNetCore.Hosting.Server;
using System.Data;
using Microsoft.CodeAnalysis;
using ClosedXML.Excel;
using static iText.IO.Util.IntHashtable;
using DocumentFormat.OpenXml.Office2010.Word;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;

namespace TaxForm.Controllers
{
    public class TrTaxesController : Controller
    {
        private readonly TaxFormReaderContext _context;

        public TrTaxesController(TaxFormReaderContext context)//, GraphServiceClient graphClient)
        {
            _context = context;
        }

        [Authorize]
        public async Task<IActionResult> ConfidentialDataAsync()
        {
            return View();
        }

        public async Task<IActionResult> Index(string search)
        {
            var userEmail = User.Identity.Name;
            var userLogin = User.Claims.ToList();
            var username = "";

            for (int i = 0; i < userLogin.Count; i++)
            {
                if (userLogin[i].Type == "name")
                {
                    username = userLogin[i].Value;
                    break;
                }
            }

            var param = _context.MsParameterValues.FirstOrDefault(p => p.Title.Contains("Tax Form") && p.Parameter.Contains("Akses Semua Tax")
                      && p.AlphaNumericValue.ToLower().Contains(userEmail));

            if (param != null)
            {
                if (search == null)
                {
                    return _context.TrTaxes != null ?
                            View(await _context.TrTaxes.Where(t => t.Status.Equals(null)).OrderByDescending(t => t.Id).ToListAsync()) :
                            Problem("Entity set 'TaxFormReaderContext.TrTaxes'  is null.");
                }
                else
                {
                    var tax = from t in _context.TrTaxes
                              select t;
                    tax = tax.Where(t => t.NomorKet.Contains(search) || t.NamaPemotong.Contains(search));

                    return _context.TrTaxes != null ?
                            View(await tax.Where(t => t.Status.Equals(null)).OrderByDescending(t => t.Id).ToListAsync()) :
                            Problem("Entity set 'TaxFormReaderContext.TrTaxes'  is null.");
                }
            }
            else
            {
                if (search == null)
                {
                    return _context.TrTaxes != null ?
                            View(await _context.TrTaxes.Where(t => t.Status.Equals(null) && t.CreatedBy.ToLower().Equals(username))
                            .OrderByDescending(t => t.Id).ToListAsync()) :
                            Problem("Entity set 'TaxFormReaderContext.TrTaxes'  is null.");
                }
                else
                {
                    var tax = from t in _context.TrTaxes
                              select t;
                    tax = tax.Where(t => t.NomorKet.Contains(search) || t.NamaPemotong.Contains(search));

                    return _context.TrTaxes != null ?
                            View(await tax.Where(t => t.Status.Equals(null) && t.CreatedBy.ToLower().Equals(username))
                            .OrderByDescending(t => t.Id).ToListAsync()) :
                            Problem("Entity set 'TaxFormReaderContext.TrTaxes'  is null.");
                }
            }
        }
        [Authorize]
        public IActionResult UploadNewTax()
        {
            return View();
        }
        [Authorize]
        public FileResult GetReport(string target)
        {
            byte[] FileBytes = System.IO.File.ReadAllBytes(target);

            return File(FileBytes, "application/pdf");
        }
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MultiUpload(TaxFilesModel model)
        {
            List<TrTax> taxModels = new List<TrTax>();
            TrTax tempTaxModel;//= new TaxModel();
            var userLogin = User.Claims.ToList();
            var username = "";

            for (int i = 0; i < userLogin.Count; i++)
            {
                if (userLogin[i].Type == "name")
                {
                    username = userLogin[i].Value;
                    break;
                }
            }
            
            try
            {
                if (model.Files != null)
            {
                if (model.Files.Count > 0)
                {                   
                        foreach (var file in model.Files)
                        {
                            string path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/Files");

                            //create folder if not exist
                            if (!Directory.Exists(path))
                                Directory.CreateDirectory(path);


                            string fileNameWithPath = Path.Combine(path, DateTime.Now.ToString("dd-MM-yyyy_HH_mm_ss") + "_" + file.FileName);

                            using (var stream = new FileStream(fileNameWithPath, FileMode.Create))
                            {
                                file.CopyTo(stream);
                            }

                            #region OCR TAX PDF FILE
                            //string path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/Files");
                            //string fileNameWithPath = Path.Combine(path, DateTime.Now.ToString("dd-MM-yyyy_HH_mm_ss") + "_" + file.FileName);
                            var pdfDocument = new PdfDocument(new PdfReader(fileNameWithPath));
                            var strategy = new LocationTextExtractionStrategy();
                            strategy.SetRightToLeftRunDirection(false);

                            for (int i = 1; i <= pdfDocument.GetNumberOfPages(); ++i)
                            {
                                tempTaxModel = new TrTax();

                                var page = pdfDocument.GetPage(i);

                                //TIDAK BISA UNTUK MULTIPLE PAGE
                                //var resultTexts = PdfTextExtractor.GetTextFromPage(page, strategy);

                                var resultTexts = PdfTextExtractor.GetTextFromPage(page);

                                string[] b = resultTexts.Split('\n');

                                var text = Encoding.UTF8.GetString(ASCIIEncoding.Convert(Encoding.Default, Encoding.UTF8, Encoding.Default.GetBytes(resultTexts)));
                                if (text.Length > 1)
                                {
                                    var resultKodeObjekPajak = Regex.Matches(text.ToLower(), @"\b[0-9]{2}-[0-9]{3}-[0-9]{2}\w*\b")
                                                    .Cast<Match>()
                                                    .Select(x => x.Value)
                                                    .ToList();
                                    var resultMasaPajak = Regex.Matches(text.ToLower(), @"\b[0-9]{2}-[0-9]{4}|[0-9]{1}-[0-9]{4}\w*\b")
                                                    .Cast<Match>()
                                                    .Select(x => x.Value)
                                                    .ToList();

                                    var results4 = string.Concat(b.Select(x => x));
                                    var tempresults4 = results4.Substring(results4.IndexOf("C.1"), results4.Length - results4.IndexOf("C.1"));
                                    var tempNamaPemotong = Encoding.UTF8.GetString(ASCIIEncoding.Convert(Encoding.Default, Encoding.UTF8, Encoding.Default.GetBytes(tempresults4)));
                                    var resultNamaPemotong = Regex.Matches(tempNamaPemotong.Replace("C.", "").Replace("Nama Wajib Pajak", "")
                                                            .Replace("\n", "").Replace("IDENTITAS PEMOTONG/PEMUNGUT", "").Replace("Tanggal", "")
                                                            .Replace(":", ""), @"\s*([.\D,]+)", RegexOptions.IgnoreCase)
                                                            .Cast<Match>()
                                                            .Select(x => x.Value).Where(x => x.Contains("A") || x.Contains("I")
                                                            || x.Contains("U") || x.Contains("E") || x.Contains("O"))
                                                            .ToList();

                                    var tempresults5 = results4.Substring(results4.IndexOf("C."), results4.Length - results4.IndexOf("C."));
                                    var tempNPWPPemotong = Encoding.UTF8.GetString(ASCIIEncoding.Convert(Encoding.Default, Encoding.UTF8, Encoding.Default.GetBytes(tempresults5)));
                                    var resultNPWPPemotong = Regex.Matches(tempNPWPPemotong.Replace("C.1", "").Replace("C.2", "")
                                                            .Replace("\n", "").Replace(" ", "").Replace("C.3", "").Replace("C.4", "")
                                                            .Replace("C.5", "").Replace(":", ""), @"\s*([0-9]+)")
                                                            .Cast<Match>()
                                                            .Select(x => x.Value)
                                                            .ToList();

                                    string resultKetPajak = "";
                                    for (int j = 0; j < b.Length; j++)
                                    {
                                        if (b[j].ToLower().Contains("keterangan"))
                                        {
                                            var tempketPajak = b[j].Replace(" ", "").IndexOf(":") + 1;
                                            if (tempketPajak == b[j].Replace(" ", "").Length)
                                            {
                                                resultKetPajak = b[j - 1];
                                            }
                                            else
                                            {
                                                resultKetPajak = b[j].Replace("B.6", "").Replace("Keterangan Kode Objek Pajak", "")
                                                .Replace(":", "");

                                                for (int k = j + 1; k < b.Length; k++)
                                                {
                                                    if (!b[k].ToLower().Contains("b.7"))
                                                    {
                                                        resultKetPajak = string.Concat(resultKetPajak, b[k]);
                                                    }
                                                    else
                                                    {
                                                        break;
                                                    }
                                                }
                                            }

                                            break;
                                        }
                                    }

                                    var results3 = results4.Substring(results4.IndexOf("B.7"), results4.Length - results4.IndexOf("C."));
                                    var temptglket = Encoding.UTF8.GetString(ASCIIEncoding.Convert(Encoding.Default, Encoding.UTF8, Encoding.Default.GetBytes(results3.Remove(results3.IndexOf("C.")))));
                                    var resultTglKet = Regex.Matches(temptglket.ToLower().Replace("b.7", "").Replace("b.8", "")
                                                    .Replace("b.9", "").Replace("b.10", "").Replace(" ", "")
                                                    .Replace("b.11", "").Replace("b.12", "").Replace("\n", "").Replace(":", ""), @"\s*([.\d,-]+)")
                                                    //.Replace("b.11", "").Replace("b.12", "").Replace(":", ""), @"\b[0-9]{2}dd[0-9]{2}mm[0-9]{4}\w*\b")
                                                    .Cast<Match>()
                                                    .Select(x => x.Value).Where(x => x.Contains("1") || x.Contains("2") || x.Contains("3") ||
                                                    x.Contains("4") || x.Contains("5") || x.Contains("6") || x.Contains("7") || x.Contains("8") ||
                                                    x.Contains("9"))
                                                    .ToList();

                                    string resultNoKet = "";
                                    for (int j = 0; j < b.Length; j++)
                                    {
                                        if (b[j].ToLower().Contains("nomor dokumen"))
                                        {
                                            var tempNoKet = b[j].Replace(" ", "").IndexOf("Nomor Dokumen");
                                            var tes = b[j].Replace("B.7", "").Replace(" ", "").Replace("DokumenReferensi", "").Replace("NomorDokumen", "").Replace(":", "");
                                            if (tes == "" && b[j - 1].Replace(" ", "").Length > 3 && !b[j].ToLower().Contains("b.7"))
                                            {
                                                resultNoKet = b[j - 1].Replace("B.7", "").Replace("Dokumen Referensi", "").Replace("Nomor Dokumen", "").Replace(":", "");
                                                break;
                                            }
                                            else if (tes != "")
                                            {
                                                resultNoKet = b[j].Replace("B.7", "").Replace("Dokumen Referensi", "").Replace("Nomor Dokumen", "").Replace(":", "");
                                                break;
                                            }
                                        }
                                        else if (b[j].ToLower().Contains("nomor faktur pajak"))
                                        {
                                            var tempNoKet = Regex.Matches(temptglket.Replace("B.7", "").Replace("B.8", "")
                                                .Replace("B.9", "").Replace("B.10", "")
                                                .Replace("B.11", "").Replace("B.12", "").Replace("\n", "").Replace(":", ""), @"\s*([.\d,-]+)")
                                                //.Replace("b.11", "").Replace("b.12", "").Replace(":", ""), @"\b[0-9]{2}dd[0-9]{2}mm[0-9]{4}\w*\b")
                                                .Cast<Match>()
                                                .Select(x => x.Value).Where(x => x.Contains("1") || x.Contains("2") || x.Contains("3") ||
                                                x.Contains("4") || x.Contains("5") || x.Contains("6") || x.Contains("7") || x.Contains("8") ||
                                                x.Contains("9"))
                                                .ToList();
                                            resultNoKet = tempNoKet[0];
                                            break;
                                        }
                                    }

                                    var resultTglBupot = Regex.Matches(tempNPWPPemotong.ToLower().Replace(" ", "").Replace("c.1", "").Replace("c.2", "")
                                                    .Replace("c.3", "").Replace(":", ""), @"\s*([0-9]+)")
                                                    .Cast<Match>()
                                                    .Select(x => x.Value)
                                                    .ToList();
                                    var resultAmount = Regex.Matches(text.ToLower(), @"\s*([.\d,]+)")
                                                    .Cast<Match>()
                                                    .Select(x => x.Value).Where(x => x.Contains(',') || x.Contains('.')).Where(x => x.Length >= 6)
                                                    .ToList();
                                    var results = Regex.Matches(text.ToLower(), @"(?sim)^pph.*?(?=(?:\r?\n){2,}|\z)")
                                                    .Cast<Match>()
                                                    .Select(x => x.Value)
                                                    .ToList();
                                    //TES FOR MASA PAJAK OR KODE OBJEK PACAR
                                    var testt = Regex.Matches(text.ToLower(), @"\s*([.\d,-]+)")
                                                    .Cast<Match>()
                                                    .Select(x => x.Value).Where(x => x.Contains('-') && (x.Contains("1") || x.Contains("2") || x.Contains("3") ||
                                                    x.Contains("4") || x.Contains("5") || x.Contains("6") || x.Contains("7") || x.Contains("8") ||
                                                    x.Contains("9")))
                                                    .ToList();
                                    if (results.Any())
                                    {
                                        #region NOMOR BUKTI POTONG
                                        string a = results[0].Replace(" ", "").Replace("\n", "");
                                        int tes = a.IndexOf("nomor:") + 6;
                                        string nobupot = a.Substring(tes, 10);
                                        if (!nobupot[0].Equals('2'))
                                        {
                                            string temp = results[0].Remove(results[0].IndexOf("\n")).Replace(" ", "");
                                            string temp1 = results[0].Replace(" ", "").Remove(0, temp.Length);
                                            nobupot = temp1.Remove(temp1.IndexOf("h.4")).Replace("nomor", "").Replace(":", "").Replace(" ", "").Replace("h.1", "");
                                        }

                                        tempTaxModel.NomorBuktiPotong = nobupot.Replace("\r", "").Replace("\n", "");
                                        //nomorBuktiPotonglabel.InnerText = nobupot;
                                        #endregion

                                        var count = 0;
                                        for (int j = 0; j < testt.Count; j++)
                                        {
                                            var temp = testt[j].Split('-');
                                            if (temp.Length == 2)
                                            {
                                                #region MASA PAJAK
                                                var temp1 = string.Concat(temp[0], "-", temp[1]).Replace("\n", "").Replace(".", "").Replace(",", "").Replace(" ", "");
                                                //masaPajaklabel.InnerText = temp1;
                                                if (temp1.Length == 6)
                                                {
                                                    temp1 = string.Concat("0", temp1);
                                                }
                                                //else if (temp1.Length >)
                                                //{

                                                //}
                                                tempTaxModel.MasaPajak = temp1;
                                                count += 1;
                                                #endregion
                                            }
                                            else
                                            {
                                                #region KODE OBJEK PAJAK

                                                var temp1 = string.Concat(temp[0], "-", temp[1], "-", temp[2]).Replace("\n", "").Replace(".", "").Replace(",", "").Replace(" ", "");
                                                if (temp1.Length > 9)
                                                {
                                                    tempTaxModel.KodeObjekPajak = temp1.Substring(0, 9);
                                                    //kodeObjekPajakabel.InnerText = temp1.Substring(0, 9);
                                                }
                                                else
                                                {
                                                    tempTaxModel.KodeObjekPajak = temp1;
                                                    //kodeObjekPajakabel.InnerText = temp1;
                                                }
                                                count += 1;
                                                #endregion
                                            }
                                            if (count == 2)
                                            {
                                                break;
                                            }
                                        }

                                        #region AMOUNT
                                        
                                        var amount = resultAmount[0].Replace(".", ",").Replace(",", ".")
                                                                    .Replace(" ", "").Replace("\n", "");
                                        var amount2 = resultAmount[1].Replace(".", ",").Replace(",", ".")
                                                                    .Replace(" ", "").Replace("\n", "");
                                        if (resultKodeObjekPajak[0].Length > 9)
                                        {
                                            //var lengthKodePajak = resultKodeObjekPajak[0].Length - 8;
                                            amount = amount.Substring(2, amount.Length - 2);
                                        }
                                        var tempamount = amount.Split('.');
                                        var tempamount2 = amount2.Split('.');
                                        if (tempamount.First().Length == 4)
                                        {
                                            amount = string.Concat(amount.Substring(2, amount.Length - 2));
                                        }
                                        else if (tempamount.First().Length == 5)
                                        {
                                            amount = string.Concat(amount.Substring(2, amount.Length - 2));
                                        }
                                        if (tempamount2.First().Length == 4)
                                        {
                                            amount2 = string.Concat(amount2.Substring(2, amount2.Length - 2));
                                        }
                                        else if (tempamount2.First().Length == 5)
                                        {
                                            amount2 = string.Concat(amount2.Substring(2, amount2.Length - 2));
                                        }
                                        //if (resultAmount[0].Length > resultAmount[1].Length)
                                        if (amount.Length > amount2.Length)
                                        {
                                            //var tempamount = amount.Split('.');
                                            //var tempamount2 = amount2.Split('.');
                                            if (tempamount.Last().Length == 2)
                                            {
                                                //amount = amount.Replace("." + tempamount.Last(), "," + tempamount.Last());
                                                amount = amount.Remove(amount.Length - 3, 3);
                                                amount = amount.Replace(".", "").Replace(",", "");
                                                //amount = string.Concat(amount + "," + tempamount.Last());
                                            }
                                            if (tempamount2.Last().Length == 2)
                                            {
                                                //amount2 = amount2.Replace("." + tempamount2.Last(), "," + tempamount2.Last());
                                                amount2 = amount2.Remove(amount2.Length - 3, 3);
                                                amount2 = amount2.Replace(".", "").Replace(",", "");
                                                //amount2 = string.Concat(amount2 + "," + tempamount2.Last());
                                            }
                                            try
                                            {
                                                tempTaxModel.DasarPengenaanPajak = decimal.Parse(amount.Replace(".", "").Replace(",", ""));
                                                tempTaxModel.Pphdipotong = decimal.Parse(amount2.Replace(".", "").Replace(",", ""));
                                            }
                                            catch (Exception)
                                            {
                                                Exception ex = new Exception();
                                                Console.WriteLine("Something went wrong: " + ex.Message);
                                                tempTaxModel.StatusPelaporan = amount + "*" + amount2;
                                                //throw new Exception();
                                            }

                                            //dasarPengenaanPajaklabel.InnerText = amount;
                                            //pphDipotonglabel.InnerText = amount2;
                                        }
                                        else
                                        {
                                            //var tempamount = amount.Split('.');
                                            //var tempamount2 = amount2.Split('.');
                                            if (tempamount.Last().Length == 2)
                                            {
                                                //amount = amount.Replace("." + tempamount.Last(), "," + tempamount.Last());
                                                amount = amount.Remove(amount.Length - 3, 3);
                                                amount = amount.Replace(".", "").Replace(",", "");
                                                //amount = string.Concat(amount + "," + tempamount.Last());
                                            }
                                            if (tempamount2.Last().Length == 2)
                                            {
                                                //amount2 = amount2.Replace("." + tempamount2.Last(), "," + tempamount2.Last());
                                                amount2 = amount2.Remove(amount2.Length - 3, 3);
                                                amount2 = amount2.Replace(".", "").Replace(",", "");
                                                //amount2 = string.Concat(amount2 + "," + tempamount2.Last());
                                            }
                                            try
                                            {
                                                tempTaxModel.DasarPengenaanPajak = decimal.Parse(amount2.Replace(".", "").Replace(",", ""));
                                                tempTaxModel.Pphdipotong = decimal.Parse(amount.Replace(".", "").Replace(",", ""));
                                            }
                                            catch (Exception)
                                            {
                                                Exception ex = new Exception();
                                                Console.WriteLine("Something went wrong: " + ex.Message);
                                                tempTaxModel.StatusPelaporan = amount + "*" + amount2;
                                                //throw new Exception();
                                            }

                                            //dasarPengenaanPajaklabel.InnerText = amount2;
                                            //pphDipotonglabel.InnerText = amount;
                                        }
                                        #endregion

                                        #region KETERANGAN KODE OBJEK PAJAK
                                        tempTaxModel.KetKodeObjekPajak = resultKetPajak.Trim();
                                        //ketKodePajaklabel.InnerText = resultKetPajak.Trim();
                                        #endregion

                                        #region TANGGAL KETERANGAN
                                        string tglket = "";
                                        int flag = 0;
                                        for (int x = 0; x < resultTglKet.Count; x++)
                                        {
                                            if (resultTglKet[x].Length == 4 && x != 0)
                                            {
                                                if (resultTglKet[x - 1].Length == 2 && (x - 1) != 0)
                                                {
                                                    if (resultTglKet[x - 2].Length == 2)
                                                    {
                                                        tglket = resultTglKet[x - 2];
                                                        tglket = string.Concat(tglket, resultTglKet[x - 1]);
                                                        tglket = string.Concat(tglket, resultTglKet[x]);
                                                        break;
                                                    }
                                                    else if (x + 1 == resultTglKet.Count)
                                                    {
                                                        flag = 1;
                                                    }
                                                }
                                                else if (x + 1 == resultTglKet.Count)
                                                {
                                                    flag = 1;
                                                }
                                            }
                                            else if (resultTglKet[x].Length == 8)
                                            {
                                                tglket = resultTglKet[x];
                                                break;
                                            }
                                            else if (x + 1 == resultTglKet.Count)
                                            {
                                                for (int y = 0; y <= resultTglKet.Count; y++)
                                                {
                                                    if (resultTglKet[y].Length > 8)
                                                    {
                                                        tglket = resultTglKet[y].Substring(resultTglKet[y].Length - 8, 8);
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                        if (flag == 1)
                                        {
                                            for (int y = 0; y <= resultTglKet.Count; y++)
                                            {
                                                if (resultTglKet[y].Length > 8)
                                                {
                                                    tglket = resultTglKet[y].Substring(resultTglKet[y].Length - 8, 8);
                                                    break;
                                                }
                                            }
                                        }
                                        tglket = string.Concat(tglket.Substring(0, 2), "-", tglket.Substring(2, 2),
                                                "-", tglket.Substring(4, 4));
                                        DateTime dt = DateTime.Now;
                                        try
                                        {
                                            dt = DateTime.ParseExact(tglket, "dd-MM-yyyy",
                                                            CultureInfo.InvariantCulture);
                                            //tglKetlabel.InnerText = dt.ToString("dd-MMM-yy");
                                        }
                                        catch (Exception)
                                        {
                                            Console.WriteLine("eror");
                                        }
                                        tempTaxModel.TanggalKet = dt;
                                        #endregion

                                        #region NOMOR KETERANGAN
                                        tempTaxModel.NomorKet = resultNoKet.Replace(" ", "").Replace("\n", "").Replace("\t", "");
                                        if (tempTaxModel.NomorKet.Length <= 2) //CASE KALAU ISI "0" / "-"
                                        {
                                            tempTaxModel.NomorKet = "-";
                                        }
                                        //noKetlabel.InnerText = resultNoKet;
                                        #endregion

                                        #region NPWP PEMOTONG
                                        var tempp = string.Concat(resultNPWPPemotong[0].Substring(0, 2), ".",
                                            resultNPWPPemotong[0].Substring(2, 3), ".", resultNPWPPemotong[0].Substring(5, 3),
                                            ".", resultNPWPPemotong[0].Substring(8, 1), "-", resultNPWPPemotong[0].Substring(9, 3),
                                            ".", resultNPWPPemotong[0].Substring(12, 3));
                                        tempTaxModel.Npwp = tempp;
                                        //npwplabel.InnerText = tempp;
                                        #endregion

                                        #region NAMA PEMOTONG
                                        tempTaxModel.NamaPemotong = resultNamaPemotong[0].Trim();
                                        //namaPemotonglabel.InnerText = resultNamaPemotong[0];
                                        #endregion

                                        #region TANGGAL BUKTI POTONG
                                        string tglbupot = "";
                                        for (int x = 0; x < resultTglBupot.Count; x++)
                                        {
                                            if (resultTglBupot[x].Length == 4 && x != 0)
                                            {
                                                if (resultTglBupot[x - 1].Length == 2)
                                                {
                                                    if (resultTglBupot[x - 2].Length == 2)
                                                    {
                                                        tglbupot = resultTglBupot[x - 2];
                                                        tglbupot = string.Concat(tglbupot, resultTglBupot[x - 1]);
                                                        tglbupot = string.Concat(tglbupot, resultTglBupot[x]);
                                                        break;
                                                    }
                                                }
                                                else if (resultTglBupot[x - 1].Length == 4)
                                                {
                                                    tglbupot = resultTglBupot[x - 1];
                                                    tglbupot = string.Concat(tglbupot, resultTglBupot[x]);
                                                    break;
                                                }
                                            }
                                            else if (resultTglBupot[x].Length == 8)
                                            {
                                                tglbupot = resultTglBupot[x];
                                                break;
                                            }
                                        }
                                        if (tglbupot.Length > 8)
                                        {
                                            tglbupot = tglbupot.Substring(0, 8);
                                            //tglBupotlabel.InnerText = tglbupot.Substring(0, 8);
                                        }
                                        //else
                                        //{
                                        //    tglbupot = tglbupot;
                                        //    //tglBupotlabel.InnerText = tglbupot;
                                        //}
                                        var temptglbupot = tglbupot;
                                        if (Int32.Parse(temptglbupot.Substring(0, 1)) > 3)
                                        {
                                            temptglbupot = string.Concat(temptglbupot.Substring(1, 1), temptglbupot.Substring(0, 1),
                                                            temptglbupot.Substring(2, 6));
                                        }
                                        if ((Int32.Parse(temptglbupot.Substring(2, 1)) > 1 && Int32.Parse(temptglbupot.Substring(3, 1)) > 2)
                                            || Int32.Parse(temptglbupot.Substring(2, 1)) > 1)
                                        {
                                            temptglbupot = string.Concat(temptglbupot.Substring(0, 2), temptglbupot.Substring(3, 1), temptglbupot.Substring(2, 1),
                                                            temptglbupot.Substring(4, 4));
                                        }
                                        temptglbupot = string.Concat(temptglbupot.Substring(0, 2), "-", temptglbupot.Substring(2, 2),
                                               "-", temptglbupot.Substring(4, 4));
                                        try
                                        {
                                            dt = DateTime.ParseExact(temptglbupot, "dd-MM-yyyy",
                                                            CultureInfo.InvariantCulture);
                                            //tglBupotlabel.InnerText = dt.ToString("dd-MMM-yy");
                                        }
                                        catch (Exception)
                                        {
                                            Console.WriteLine("eror");
                                        }
                                        tempTaxModel.TanggalBuktiPotong = dt;
                                        #endregion

                                    }
                                    //}
                                    tempTaxModel.CreatedDate = DateTime.Now;
                                    tempTaxModel.ModifiedDate = DateTime.Now;
                                    tempTaxModel.CreatedBy = username;
                                    tempTaxModel.ModifiedBy = username;
                                    //tempTaxModel.StatusPph = "PPH Tidak Final";
                                    var tempstatuspph = tempTaxModel.KodeObjekPajak.Split("-");
                                    if (tempstatuspph[0] == "24")
                                    {
                                        tempTaxModel.StatusPph = "PPh 23";
                                    }
                                    else if (tempstatuspph[0] == "22")
                                    {
                                        tempTaxModel.StatusPph = "PPh 22";
                                    }
                                    else if (tempstatuspph[0] == "27")
                                    {
                                        tempTaxModel.StatusPph = "PPh 26";
                                    }
                                    else if (tempstatuspph[0] == "28")
                                    {
                                        tempTaxModel.StatusPph = "PPh Final";
                                    }
                                    tempTaxModel.DocumentId = tempTaxModel.NomorKet;
                                    tempTaxModel.FileName = file.FileName;
                                    tempTaxModel.FilePath = fileNameWithPath;

                                    var tax = from t in _context.TrTaxes
                                              select t;
                                    tax = tax.Where(t => t.NomorKet.Contains(tempTaxModel.NomorKet) &&
                                            t.NomorBuktiPotong.Contains(tempTaxModel.NomorBuktiPotong) &&
                                            t.Npwp.Contains(tempTaxModel.Npwp) &&
                                            t.Pphdipotong.Equals(tempTaxModel.Pphdipotong) &&
                                            t.TanggalBuktiPotong.Equals(tempTaxModel.TanggalBuktiPotong) &&
                                            t.Status.Equals(null));
                                    var taxs = await tax.OrderByDescending(t => t.CreatedDate).ToListAsync();

                                    //_context.Add(tempTaxModel);

                                    if (taxs.Count > 0)
                                    {
                                       
                                        ViewBag.Message = string.Concat(ViewBag.Message + "Nomor Bupot: " + tempTaxModel.NomorBuktiPotong +
                                                                                        " dengan Nomor Dokumen: " + tempTaxModel.NomorKet + " Sudah Ada~");
                                        
                                        
                                    }
                                    else
                                    {
                                        _context.Add(tempTaxModel);
                                        await _context.SaveChangesAsync();
                                    }
                                    //taxModels.Add(tempTaxModel);
                                }
                                else
                                {
                                    ViewBag.Message += "Isi File " + file.FileName + " tidak bisa berupa gambar/foto~";
                                    
                                }
                            }

                            pdfDocument.Close();

                            #endregion
                        }
                }
            }
            
            }
            catch (Exception)
            {
                Exception exec = new Exception();
                ViewBag.Message += "Terjadi error saat mengupload File.";
            }

            if (ViewBag.Message == null)
            {
                ViewBag.Message = "File telah berhasil diupload. Klik tombol Back untuk melihat hasil OCR Scan.";
            }

            return View("UploadNewTax", model);
            
        }
        [Authorize]
        public async Task<IActionResult> ViewExportExcel(string search, DateTime? startdate, DateTime? enddate)
        {
            var start = startdate;
            var end = enddate;

            var userEmail = User.Identity.Name;
            var userLogin = User.Claims.ToList();
            var username = "";

            for (int i = 0; i < userLogin.Count; i++)
            {
                if (userLogin[i].Type == "name")
                {
                    username = userLogin[i].Value;
                    break;
                }
            }
            var param = _context.MsParameterValues.FirstOrDefault(p => p.Title.Contains("Tax Form") && p.Parameter.Contains("Akses Semua Tax")
                                  && p.AlphaNumericValue.ToLower().Contains(userEmail));

            if (param != null)
            { 
                if (search == null && (start == null && end == null))
                {
                    return _context.TrTaxes != null ?
                            View(await _context.TrTaxes.Where(t => t.Status.Equals(null)).OrderByDescending(t => t.Id).ToListAsync()) :
                            Problem("Entity set 'TaxFormReaderContext.TrTaxes'  is null.");
                }
                else
                {
                    var tax = from t in _context.TrTaxes
                              select t;
                    if (search != null)
                    {
                        tax = tax.Where(t => t.NomorKet.Contains(search) || t.NamaPemotong.Contains(search));
                    }

                    if (start != null && end != null)
                    {
                        tax = tax.Where(t => t.CreatedDate >= start && t.CreatedDate <= end);
                    }

                    return _context.TrTaxes != null ?
                            View(await tax.Where(t => t.Status.Equals(null)).OrderByDescending(t => t.Id).ToListAsync()) :
                            Problem("Entity set 'TaxFormReaderContext.TrTaxes'  is null.");
                }
            }
            else
            {
                if (search == null && (start == null && end == null))
                {
                    return _context.TrTaxes != null ?
                            View(await _context.TrTaxes.Where(t => t.Status.Equals(null) && t.CreatedBy.ToLower().Equals(username))
                            .OrderByDescending(t => t.Id).ToListAsync()) :
                            Problem("Entity set 'TaxFormReaderContext.TrTaxes'  is null.");
                }
                else
                {
                    var tax = from t in _context.TrTaxes
                              select t;
                    if (search != null)
                    {
                        tax = tax.Where(t => t.NomorKet.Contains(search) || t.NamaPemotong.Contains(search));
                    }

                    if (start != null && end != null)
                    {
                        tax = tax.Where(t => t.CreatedDate >= start && t.CreatedDate <= end);
                    }

                    return _context.TrTaxes != null ?
                            View(await tax.Where(t => t.Status.Equals(null) && t.CreatedBy.ToLower().Equals(username))
                            .OrderByDescending(t => t.Id).ToListAsync()) :
                            Problem("Entity set 'TaxFormReaderContext.TrTaxes'  is null.");
                }
            }
        }
        [Authorize]
        public async Task<IActionResult> ExportExcel(string[] IsSelected)
        {
            // Preparing file download
            string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            string fileName = "ReportBuktiPotong_" + DateTime.Now.ToShortDateString() + ".xlsx";

            string DMY_his_format = "dd-MMM-yy";
            string format_nominal = "#,###";

            try
            {
                using (var workbook = new XLWorkbook())
                {
                    // # Sheet: Overview
                    var shBupot = workbook.Worksheets.Add("Bukti Potong");

                    // Title of Bupot
                    shBupot.Cell(1, 1).Value = "LAMPIRAN II";
                    shBupot.Cell(2, 1).Value = "PPH PASAL 23";
                    shBupot.Cell(2, 3).Value = "ENGEMENT";
                    shBupot.Cell(1, 1).Style.Font.Bold = true;
                    shBupot.Row(2).Style.Font.Bold = true;

                    // Header of Bupot
                    shBupot.Cell(5, 1).Value = "NO";
                    shBupot.Cell(5, 2).Value = "NAMA PEMOTONG PAJAK";
                    shBupot.Cell(5, 3).Value = "NPWP PEMOTONG PAJAK";
                    shBupot.Cell(5, 4).Value = "JENIS PENGHASILAN";
                    shBupot.Cell(5, 5).Value = "KODE PAJAK";
                    shBupot.Cell(5, 6).Value = "RUPIAH";
                    shBupot.Cell(5, 7).Value = "PAJAK PENGHASILAN YANG DIPOTONG / DIPUNGUT(RUPIAH)";
                    shBupot.Cell(5, 8).Value = "MASA PAJAK";
                    shBupot.Cell(5, 9).Value = "NOMOR BUKTI POTONG";
                    shBupot.Cell(5, 10).Value = "TANGGAL BUKTI POTONG";
                    shBupot.Cell(5, 11).Value = "NOMOR KETERANGAN";
                    shBupot.Cell(5, 12).Value = "TANGGAL KETERANGAN";
                    shBupot.Cell(5, 13).Value = "STATUS PPH";
                    shBupot.Row(5).Style.Font.Bold = true;
                    shBupot.Column(6).Style.NumberFormat.SetNumberFormatId(4);
                    shBupot.Column(7).Style.NumberFormat.SetNumberFormatId(4);
                    // List of Bupot
                    var tax = from t in _context.TrTaxes
                              select t;
                    for (int index = 1; index <= IsSelected.Length; index++)
                    {
                        string[] temp = IsSelected[index - 1].Split("|");
                        int id = Int32.Parse(temp[0]);
                        string bupot = temp[1];
                        var item = tax.Where(t => t.Id.Equals(id) && t.NomorBuktiPotong.Equals(bupot))
                            .FirstOrDefault();
                        shBupot.Cell(5 + index, 1).Value = index;
                        shBupot.Cell(5 + index, 2).Value = item.NamaPemotong;
                        shBupot.Cell(5 + index, 3).Value = item.Npwp;
                        shBupot.Cell(5 + index, 4).Value = item.KetKodeObjekPajak;
                        shBupot.Cell(5 + index, 5).Value = item.KodeObjekPajak;
                        shBupot.Cell(5 + index, 6).Value = item.DasarPengenaanPajak;
                        shBupot.Cell(5 + index, 7).Value = item.Pphdipotong;
                        shBupot.Cell(5 + index, 8).Value = item.MasaPajak;
                        shBupot.Cell(5 + index, 9).Value = item.NomorBuktiPotong;
                        shBupot.Cell(5 + index, 10).Value = item.TanggalBuktiPotong.ToString(DMY_his_format);
                        shBupot.Cell(5 + index, 11).Value = item.NomorKet;
                        shBupot.Cell(5 + index, 12).Value = item.TanggalKet.ToString(DMY_his_format);
                        shBupot.Cell(5 + index, 13).Value = item.StatusPph;
                    }
                    int length = IsSelected.Length + 5;
                    shBupot.Range("A5:M" + length).CreateTable();
                    shBupot.Columns().AdjustToContents(5);

                    using (var stream = new System.IO.MemoryStream())
                    {
                        workbook.SaveAs(stream);

                        return File(stream.ToArray(), contentType, fileName);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Something went wrong: " + ex.Message);
                throw new Exception();
            }
        }
        [Authorize]
        public async Task<IActionResult> ViewDetail(int id)
        {
            if (id < 1 || _context.TrTaxes == null)
            {
                return NotFound();
            }

            var trTax = await _context.TrTaxes
                .FirstOrDefaultAsync(m => m.Id == id);
            if (trTax == null)
            {
                return NotFound();
            }

            ViewBag.Date = trTax.TanggalBuktiPotong.Date;
            return View(trTax);
        }
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("DocumentId,DasarPengenaanPajak,KetKodeObjekPajak,KodeObjekPajak,MasaPajak,NamaPemotong,NomorBuktiPotong,NomorKet,Npwp,Pphdipotong,TanggalBuktiPotong,TanggalKet,StatusPph")] TrTax trTax)
        {
            var userLogin = User.Claims.ToList();
            var username = "";

            ViewBag.Message = "Masuk Edit";
            for (int i = 0; i < userLogin.Count; i++)
            {
                if (userLogin[i].Type == "name")
                {
                    username = userLogin[i].Value;
                    break;
                }
            }
            if (_context.TrTaxes.Where(x => x.Id == id).Count() == 0)
            {
                return NotFound();
            }

                try
                {
                    var tax = _context.TrTaxes.FirstOrDefault(s => s.Id.Equals(id));

                    if (tax != null)
                    {
                        tax.ModifiedBy = trTax.ModifiedBy;
                        tax.ModifiedDate = trTax.ModifiedDate;
                        tax.StatusPph = trTax.StatusPph;
                        tax.MasaPajak = trTax.MasaPajak;
                        tax.KetKodeObjekPajak = trTax.KetKodeObjekPajak;
                        tax.KodeObjekPajak = trTax.KodeObjekPajak;
                        tax.DasarPengenaanPajak = trTax.DasarPengenaanPajak;
                        tax.NamaPemotong = trTax.NamaPemotong;
                        tax.NomorBuktiPotong = trTax.NomorBuktiPotong;
                        tax.NomorKet = trTax.NomorKet;
                        tax.Npwp = trTax.Npwp;
                        tax.Pphdipotong = trTax.Pphdipotong;
                        tax.TanggalBuktiPotong = trTax.TanggalBuktiPotong;
                        tax.TanggalKet = trTax.TanggalKet;
                        tax.ModifiedDate = DateTime.Now;
                        tax.ModifiedBy = username;
                    }

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    DbUpdateConcurrencyException exec = new DbUpdateConcurrencyException();
                    if (!TrTaxExists(trTax.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            return RedirectToAction(nameof(Index));
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Delete (int id, string DocumentId, string Remarks, 
            [Bind("DocumentId, Id, Remarks")] TrTax trTax)
        {
            var userLogin = User.Claims.ToList();
            var username = "";

            for (int i = 0; i < userLogin.Count; i++)
            {
                if (userLogin[i].Type == "name")
                {
                    username = userLogin[i].Value;
                    break;
                }
            }

            if (_context.TrTaxes.Where(x => x.Id == id).Count() == 0)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var tax = _context.TrTaxes.FirstOrDefault(s => s.Id.Equals(id) && s.DocumentId.Contains(DocumentId));

                    if (tax != null)
                    {
                        tax.Status = "Deleted";
                        tax.Remarks = Remarks;
                        tax.ModifiedDate = DateTime.Now;
                        tax.ModifiedBy = username;
                    }

                    await _context.SaveChangesAsync();

                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TrTaxExists(trTax.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            return RedirectToAction(nameof(Index));
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private bool TrTaxExists(int id)
        {
            return (_context.TrTaxes?.Any(e => e.Id == id)).GetValueOrDefault();
        }
    }
}
