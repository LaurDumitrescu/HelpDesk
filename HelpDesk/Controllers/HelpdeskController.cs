using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using HelpdeskApp.Data;
using HelpdeskApp.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.IO;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace HelpdeskApp.Controllers
{
    [Authorize]
    public class HelpdeskController : Controller
    {
        private readonly HelpdeskContext _context;
        private readonly ILogger<HelpdeskController> _logger;
        private const int PageSize = 30;

        public HelpdeskController(HelpdeskContext context, ILogger<HelpdeskController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            _logger.LogInformation("Index page requested");
            var currentUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Nume == HttpContext.Session.GetString("Username"));
            ViewBag.UserName = currentUser?.Name;

            if (currentUser == null)
            {
                _logger.LogWarning("User not found in session.");
            }

            return View();
        }

        public async Task<IActionResult> ViewEntries(
    int page = 1,
    string filterFirma = null,
    string filterPctLucru = null,
    string filterNrTelefon = null,
    DateTime? startDate = null,
    DateTime? endDate = null,
    string filterInTimpulProgramului = null) // New parameter
        {
            _logger.LogInformation("ViewEntries method called with parameters: page={Page}, filterFirma={FilterFirma}, filterPctLucru={FilterPctLucru}, filterNrTelefon={FilterNrTelefon}, startDate={StartDate}, endDate={EndDate}, filterInTimpulProgramului={FilterInTimpulProgramului}.", page, filterFirma, filterPctLucru, filterNrTelefon, startDate, endDate, filterInTimpulProgramului);

            var currentUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Nume == HttpContext.Session.GetString("Username"));
            if (currentUser != null)
            {
                ViewBag.UserName = currentUser.Name;
            }
            else
            {
                _logger.LogWarning("Current user not found in session.");
                return Unauthorized();
            }

            var query = _context.HelpdeskEntries
                .Include(e => e.FirmaNrTelefon)
                .ThenInclude(f => f.FirmaPunctLucru)
                .AsQueryable();

            if (!string.IsNullOrEmpty(filterFirma))
            {
                query = query.Where(e => e.FirmaNrTelefon.FirmaPunctLucru.Firma.Contains(filterFirma));
            }

            if (!string.IsNullOrEmpty(filterPctLucru))
            {
                query = query.Where(e => e.FirmaNrTelefon.FirmaPunctLucru.PctLucru.Contains(filterPctLucru));
            }

            if (!string.IsNullOrEmpty(filterNrTelefon))
            {
                query = query.Where(e => e.FirmaNrTelefon.NrTelefon.Contains(filterNrTelefon));
            }

            if (startDate.HasValue)
            {
                query = query.Where(e => e.Data >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(e => e.Data <= endDate.Value);
            }

            // Fetch entries from the database
            var entries = await query
                .OrderByDescending(e => e.Data)
                .ThenByDescending(e => e.OraApel)
                .Select(e => new
                {
                    e.Id,
                    Firma = e.FirmaNrTelefon.FirmaPunctLucru.Firma ?? "",
                    PctLucru = e.FirmaNrTelefon.FirmaPunctLucru.PctLucru ?? "",
                    NrTelefon = e.FirmaNrTelefon.NrTelefon ?? "",
                    Data = e.Data,
                    Zi = e.Zi ?? "",
                    OraApel = e.OraApel,
                    DurataApel = e.DurataApel ?? "",
                    Problema = e.Problema ?? "",
                    Rezolvare = e.Rezolvare ?? "",
                    InsUserId = e.InsUserId,
                    ModUserId = e.ModUserId
                })
                .ToListAsync(); // Fetch all results to be filtered in memory

            // Step 2: Filter client-side
            if (!string.IsNullOrEmpty(filterInTimpulProgramului))
            {
                bool isDuringSchedule = filterInTimpulProgramului == "Da";

                entries = entries.Where(e =>
                {
                    var oraApelDateTime = e.Data + e.OraApel;
                    var isWeekend = oraApelDateTime.DayOfWeek == DayOfWeek.Saturday || oraApelDateTime.DayOfWeek == DayOfWeek.Sunday;
                    var isWithinWorkingHours = e.OraApel >= TimeSpan.FromHours(8) && e.OraApel < TimeSpan.FromHours(19);
                    return isDuringSchedule == (!isWeekend && isWithinWorkingHours);
                }).ToList();
            }

            var totalEntries = entries.Count;
            var totalPages = (int)Math.Ceiling(totalEntries / (double)PageSize);

            // Step 3: Apply pagination after client-side filtering
            entries = entries.Skip((page - 1) * PageSize)
                             .Take(PageSize)
                             .ToList();

            var insUserIds = entries.Where(e => e.InsUserId.HasValue).Select(e => e.InsUserId.Value).Distinct();
            var modUserIds = entries.Where(e => e.ModUserId.HasValue).Select(e => e.ModUserId.Value).Distinct();
            var userIds = insUserIds.Concat(modUserIds).Distinct();

            var users = await _context.Users
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Name);

            var result = entries.Select(e => new
            {
                e.Id,
                e.Firma,
                e.PctLucru,
                e.NrTelefon,
                Data = e.Data.ToString("yyyy-MM-dd"), // Ensure Data is formatted as a string
                e.Zi,
                OraApel = e.OraApel.ToString(@"hh\:mm\:ss") ?? "",
                e.DurataApel,
                e.Problema,
                e.Rezolvare,
                InseratDe = e.InsUserId.HasValue && users.ContainsKey(e.InsUserId.Value) ? users[e.InsUserId.Value] : "",
                ModificatDe = e.ModUserId.HasValue && users.ContainsKey(e.ModUserId.Value) ? users[e.ModUserId.Value] : ""
            }).ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.FilterFirma = filterFirma;
            ViewBag.FilterPctLucru = filterPctLucru;
            ViewBag.FilterNrTelefon = filterNrTelefon;
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
            ViewBag.FilterInTimpulProgramului = filterInTimpulProgramului; // Pass the selected value back to the view

            _logger.LogInformation("ViewEntries method succeeded: Retrieved {TotalEntries} entries.", totalEntries);

            return View(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create(string Firma, string PctLucru, string NrTelefon, DateTime Data, string Zi, string OraApel, string DurataApel, string Problema, string Rezolvare)
        {
            _logger.LogInformation("Create method called with parameters: Firma={Firma}, PctLucru={PctLucru}, NrTelefon={NrTelefon}, Data={Data}, Zi={Zi}, OraApel={OraApel}, DurataApel={DurataApel}, Problema={Problema}, Rezolvare={Rezolvare}", Firma, PctLucru, NrTelefon, Data, Zi, OraApel, DurataApel, Problema, Rezolvare);

            var currentUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Nume == HttpContext.Session.GetString("Username"));

            if (currentUser == null)
            {
                ModelState.AddModelError(string.Empty, "User not found.");
                _logger.LogWarning("User not found in session.");
                return View("Index");
            }

            // Validate mandatory fields
            if (string.IsNullOrWhiteSpace(Firma) || string.IsNullOrWhiteSpace(PctLucru) || string.IsNullOrWhiteSpace(NrTelefon) || Data == default || string.IsNullOrWhiteSpace(OraApel) || string.IsNullOrWhiteSpace(DurataApel))
            {
                ModelState.AddModelError(string.Empty, "All fields marked with * are mandatory.");
                _logger.LogWarning("Mandatory fields are missing.");
                return View("Index");
            }

            // Validate NrTelefon
            if (NrTelefon.Length != 10 || !NrTelefon.All(char.IsDigit))
            {
                ModelState.AddModelError(string.Empty, "Numar Telefon must be exactly 10 digits.");
                _logger.LogWarning("Invalid Numar Telefon provided.");
                return View("Index");
            }

            // Validate OraApel
            if (!TimeSpan.TryParse(OraApel, out TimeSpan oraApelTime))
            {
                ModelState.AddModelError(string.Empty, "Ora Apel must be a valid time.");
                _logger.LogWarning("Invalid Ora Apel provided.");
                return View("Index");
            }

            // Check if OraApel is in the future relative to the system time
            var currentDateTime = DateTime.Now;
            var selectedDateTime = Data.Date.Add(oraApelTime);
            if (selectedDateTime > currentDateTime)
            {
                ModelState.AddModelError(string.Empty, "Ora Apel cannot be in the future.");
                _logger.LogWarning("Ora Apel is in the future.");
                return View("Index");
            }

            var firmaPunctLucru = await _context.FirmaPunctLucruEntries
                .FirstOrDefaultAsync(f => f.Firma == Firma && f.PctLucru == PctLucru);

            if (firmaPunctLucru == null)
            {
                var existingPriority = await _context.FirmaPunctLucruEntries
                    .Where(f => f.Firma == Firma)
                    .Select(f => f.Priority)
                    .FirstOrDefaultAsync();

                firmaPunctLucru = new FirmaPunctLucru
                {
                    Firma = Firma,
                    PctLucru = PctLucru,
                    Priority = existingPriority != 0 ? existingPriority : 0
                };
                _context.FirmaPunctLucruEntries.Add(firmaPunctLucru);
                await _context.SaveChangesAsync();
                _logger.LogInformation("New FirmaPunctLucru created: {Firma}, {PctLucru}", Firma, PctLucru);
            }

            FirmaNrTelefon firmaNrTelefon = await _context.FirmaNrTelefonEntries
                .FirstOrDefaultAsync(f => f.NrTelefon == NrTelefon && f.ID_firma_punct_lucru == firmaPunctLucru.Id);

            if (firmaNrTelefon == null)
            {
                firmaNrTelefon = new FirmaNrTelefon
                {
                    ID_firma_punct_lucru = firmaPunctLucru.Id,
                    NrTelefon = NrTelefon
                };
                _context.FirmaNrTelefonEntries.Add(firmaNrTelefon);
                await _context.SaveChangesAsync();
                _logger.LogInformation("New FirmaNrTelefon created: {NrTelefon}", NrTelefon);
            }

            var entry = new HelpdeskEntry
            {
                ID_nr_telefon = firmaNrTelefon?.Id ?? default(long),
                Data = Data,
                Zi = Zi,
                OraApel = oraApelTime,
                DurataApel = DurataApel,
                Problema = Problema,
                Rezolvare = Rezolvare,
                InsTime = DateTime.Now,
                InsUserId = currentUser.Id
            };

            _context.HelpdeskEntries.Add(entry);
            await _context.SaveChangesAsync();

            ViewBag.SuccessMessage = "Apel adaugat cu succes!";
            _logger.LogInformation("HelpdeskEntry created successfully with ID: {EntryId}", entry.Id);
            return View("Index");
        }


        public async Task<IActionResult> GetEntryDetails(long id)
        {
            var entry = await _context.HelpdeskEntries
                .Include(e => e.FirmaNrTelefon)
                .ThenInclude(f => f.FirmaPunctLucru)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (entry == null)
            {
                return NotFound();
            }

            var insUser = await _context.Users.FindAsync(entry.InsUserId);
            var modUser = await _context.Users.FindAsync(entry.ModUserId);

            var details = new
            {
                entry.Id,
                Firma = entry.FirmaNrTelefon.FirmaPunctLucru.Firma,
                PctLucru = entry.FirmaNrTelefon.FirmaPunctLucru.PctLucru,
                Priority = entry.FirmaNrTelefon.FirmaPunctLucru.Priority,
                NrTelefon = entry.FirmaNrTelefon.NrTelefon,
                entry.Data,
                entry.Zi,
                entry.OraApel,
                entry.DurataApel,
                entry.Problema,
                entry.Rezolvare,
                entry.InsTime,
                entry.ModTime,
                InsUserName = insUser?.Name,
                ModUserName = modUser?.Name
            };

            return Json(details);
        }


        public async Task<IActionResult> ModifyEntry(long id)
        {
            var entry = await _context.HelpdeskEntries
                .Include(e => e.FirmaNrTelefon)
                .ThenInclude(f => f.FirmaPunctLucru)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (entry == null)
            {
                return NotFound();
            }

            var model = new EntryModel
            {
                Id = entry.Id,
                Firma = entry.FirmaNrTelefon.FirmaPunctLucru.Firma,
                PctLucru = entry.FirmaNrTelefon.FirmaPunctLucru.PctLucru,
                NrTelefon = entry.FirmaNrTelefon.NrTelefon,
                Data = entry.Data,
                OraApel = entry.OraApel.ToString(@"hh\:mm\:ss"),
                DurataApel = entry.DurataApel,
                Problema = entry.Problema,
                Rezolvare = entry.Rezolvare
            };

            ViewBag.UserName = await GetUserName();
            return View(model);
        }


        private async Task<string> GetUserName()
        {
            var currentUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Nume == HttpContext.Session.GetString("Username"));

            return currentUser?.Name;
        }


        [HttpPost]
        public async Task<IActionResult> UpdateField([FromBody] UpdateFieldRequest request)
        {
            if (request == null)
            {
                _logger.LogError("UpdateField method failed: Request is null.");
                return BadRequest("Request cannot be null.");
            }

            if (request.Fields == null)
            {
                _logger.LogError("UpdateField method failed: Request fields are null.");
                return BadRequest("Request fields cannot be null.");
            }

            var entry = await _context.HelpdeskEntries
                .Include(e => e.FirmaNrTelefon)
                .ThenInclude(f => f.FirmaPunctLucru)
                .FirstOrDefaultAsync(e => e.Id == request.Id);

            if (entry == null)
            {
                _logger.LogError("UpdateField method failed: Entry with ID {EntryId} not found.", request.Id);
                return NotFound("Entry not found.");
            }

            var currentUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Nume == HttpContext.Session.GetString("Username"));

            if (currentUser == null)
            {
                _logger.LogWarning("UpdateField method failed: Current user not found in session.");
                return Unauthorized("Current user not found.");
            }

            string firma = request.Fields.ContainsKey("Firma") ? request.Fields["Firma"] : entry.FirmaNrTelefon.FirmaPunctLucru.Firma;
            string pctLucru = request.Fields.ContainsKey("PctLucru") ? request.Fields["PctLucru"] : entry.FirmaNrTelefon.FirmaPunctLucru.PctLucru;
            string nrTelefon = request.Fields.ContainsKey("NrTelefon") ? request.Fields["NrTelefon"] : entry.FirmaNrTelefon.NrTelefon;

            foreach (var field in request.Fields)
            {
                switch (field.Key)
                {
                    case "Firma":
                        if (string.IsNullOrWhiteSpace(field.Value))
                        {
                            return BadRequest("Firma cannot be empty.");
                        }
                        firma = field.Value;
                        break;

                    case "PctLucru":
                        if (string.IsNullOrWhiteSpace(field.Value))
                        {
                            return BadRequest("Pct Lucru cannot be empty.");
                        }
                        pctLucru = field.Value;
                        break;

                    case "NrTelefon":
                        if (string.IsNullOrWhiteSpace(field.Value))
                        {
                            return BadRequest("Numar Telefon cannot be empty.");
                        }
                        nrTelefon = field.Value;
                        break;

                    case "Data":
                        if (string.IsNullOrWhiteSpace(field.Value) || !DateTime.TryParse(field.Value, out var dateValue))
                        {
                            return BadRequest("Invalid Date.");
                        }
                        entry.Data = dateValue;
                        entry.Zi = dateValue.DayOfWeek.ToString();
                        break;

                    case "OraApel":
                        if (string.IsNullOrWhiteSpace(field.Value) || !TimeSpan.TryParse(field.Value, out var timeValue))
                        {
                            return BadRequest("Invalid Ora Apel.");
                        }
                        entry.OraApel = timeValue;
                        break;

                    case "DurataApel":
                        if (string.IsNullOrWhiteSpace(field.Value) || !TimeSpan.TryParse(field.Value, out var durationValue))
                        {
                            return BadRequest("Invalid Durata Apel.");
                        }
                        entry.DurataApel = field.Value;
                        break;

                    case "Problema":
                        entry.Problema = string.IsNullOrWhiteSpace(field.Value) ? null : field.Value;
                        break;

                    case "Rezolvare":
                        entry.Rezolvare = string.IsNullOrWhiteSpace(field.Value) ? null : field.Value;
                        break;
                }
            }

            // Check if the updated firma and pctLucru exist
            var existingFirmaPunctLucru = await _context.FirmaPunctLucruEntries
                .FirstOrDefaultAsync(f => f.Firma == firma && f.PctLucru == pctLucru);

            if (existingFirmaPunctLucru == null)
            {
                var newFirmaPunctLucru = new FirmaPunctLucru
                {
                    Firma = firma,
                    PctLucru = pctLucru
                };
                _context.FirmaPunctLucruEntries.Add(newFirmaPunctLucru);
                await _context.SaveChangesAsync();
                existingFirmaPunctLucru = newFirmaPunctLucru;
            }

            // Check if the nrTelefon exists for the given firma and pctLucru
            var existingNrTelefon = await _context.FirmaNrTelefonEntries
                .FirstOrDefaultAsync(f => f.NrTelefon == nrTelefon && f.ID_firma_punct_lucru == existingFirmaPunctLucru.Id);

            if (existingNrTelefon == null)
            {
                var newNrTelefon = new FirmaNrTelefon
                {
                    ID_firma_punct_lucru = existingFirmaPunctLucru.Id,
                    NrTelefon = nrTelefon
                };
                _context.FirmaNrTelefonEntries.Add(newNrTelefon);
                await _context.SaveChangesAsync();
                existingNrTelefon = newNrTelefon;
            }

            // Update the entry with the ID_nr_telefon
            entry.ID_nr_telefon = existingNrTelefon.Id;
            entry.ModTime = DateTime.Now;
            entry.ModUserId = currentUser.Id;

            _context.HelpdeskEntries.Update(entry);
            await _context.SaveChangesAsync();

            _logger.LogInformation("HelpdeskEntry updated successfully with ID: {EntryId}", entry.Id);

            // Set TempData to indicate a successful modification
            TempData["ModificationSuccess"] = true;

            var modifiedUserName = currentUser.Nume;
            return Json(new { modifiedUserName });
        }

        [HttpGet]
        public async Task<IActionResult> GetSuggestions(string term, string field, string firma = null, string pctLucru = null)
        {
            _logger.LogInformation("GetSuggestions method called with parameters: term={Term}, field={Field}, firma={Firma}, pctLucru={PctLucru}", term, field, firma, pctLucru);

            if (string.IsNullOrWhiteSpace(term) || string.IsNullOrWhiteSpace(field))
            {
                _logger.LogWarning("GetSuggestions method failed: Invalid input.");
                return BadRequest("Invalid input");
            }

            if (field == "Firma")
            {
                var suggestions = await _context.FirmaPunctLucruEntries
                    .FromSqlRaw("SELECT DISTINCT [firma] FROM [HelpDesk].[dbo].[firma_punct_lucru] WHERE [firma] LIKE {0}", $"%{term}%")
                    .Select(f => f.Firma)
                    .ToListAsync();
                return Json(suggestions);
            }
            else if (field == "PctLucru")
            {
                if (string.IsNullOrWhiteSpace(firma))
                {
                    _logger.LogWarning("GetSuggestions method failed: Firma is required for PctLucru suggestions.");
                    return BadRequest("Firma is required for PctLucru suggestions.");
                }

                var suggestions = await _context.FirmaPunctLucruEntries
                    .FromSqlRaw("SELECT DISTINCT [pct_lucru] FROM [HelpDesk].[dbo].[firma_punct_lucru] WHERE [pct_lucru] LIKE {0} AND [firma] = {1}", $"%{term}%", firma)
                    .Select(f => f.PctLucru)
                    .ToListAsync();
                return Json(suggestions);
            }
            else if (field == "NrTelefon")
            {
                if (string.IsNullOrWhiteSpace(firma) || string.IsNullOrWhiteSpace(pctLucru))
                {
                    _logger.LogWarning("GetSuggestions method failed: Firma and PctLucru are required for NrTelefon suggestions.");
                    return BadRequest("Firma and PctLucru are required for NrTelefon suggestions.");
                }

                var suggestions = await _context.FirmaNrTelefonEntries
                    .Include(f => f.FirmaPunctLucru)
                    .Where(f => f.NrTelefon.Contains(term) && f.FirmaPunctLucru.Firma == firma && f.FirmaPunctLucru.PctLucru == pctLucru)
                    .Select(f => f.NrTelefon)
                    .Distinct()
                    .ToListAsync();
                return Json(suggestions);
            }

            _logger.LogWarning("GetSuggestions method failed: Invalid field.");
            return BadRequest("Invalid field");
        }

        [HttpGet]
        public async Task<IActionResult> GetNumarTelefonSuggestions(string term)
        {
            _logger.LogInformation("GetNumarTelefonSuggestions method called with parameters: term={Term}", term);

            if (string.IsNullOrWhiteSpace(term))
            {
                _logger.LogWarning("GetNumarTelefonSuggestions method failed: Invalid input.");
                return BadRequest("Invalid input");
            }

            var suggestions = await _context.FirmaNrTelefonEntries
                .Where(f => f.NrTelefon.Contains(term))
                .Select(f => f.NrTelefon)
                .Distinct()
                .ToListAsync();

            _logger.LogInformation("GetNumarTelefonSuggestions method succeeded: {Count} suggestions found.", suggestions.Count);
            return Json(suggestions);
        }

        [HttpGet]
        public async Task<IActionResult> ExportToExcel(string filterFirma, string filterPctLucru, string filterNrTelefon, DateTime? startDate, DateTime? endDate)
        {
            _logger.LogInformation("ExportToExcel method called with parameters: filterFirma={FilterFirma}, filterPctLucru={FilterPctLucru}, filterNrTelefon={FilterNrTelefon}, startDate={StartDate}, endDate={EndDate}", filterFirma, filterPctLucru, filterNrTelefon, startDate, endDate);

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            var query = _context.HelpdeskEntries
                .Include(e => e.FirmaNrTelefon)
                .ThenInclude(f => f.FirmaPunctLucru)
                .AsQueryable();

            if (!string.IsNullOrEmpty(filterFirma))
            {
                query = query.Where(e => e.FirmaNrTelefon.FirmaPunctLucru.Firma.Contains(filterFirma));
            }

            if (!string.IsNullOrEmpty(filterPctLucru))
            {
                query = query.Where(e => e.FirmaNrTelefon.FirmaPunctLucru.PctLucru.Contains(filterPctLucru));
            }

            if (!string.IsNullOrEmpty(filterNrTelefon))
            {
                query = query.Where(e => e.FirmaNrTelefon.NrTelefon.Contains(filterNrTelefon));
            }

            if (startDate.HasValue)
            {
                query = query.Where(e => e.Data >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(e => e.Data <= endDate.Value);
            }

            query = query.OrderByDescending(e => e.Data).ThenByDescending(e => e.OraApel);

            var entries = await query.ToListAsync();

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Entries");

                worksheet.Cells["A1"].Value = "Firma";
                worksheet.Cells["B1"].Value = "Punct de Lucru";
                worksheet.Cells["C1"].Value = "Numar Telefon";
                worksheet.Cells["D1"].Value = "Data";
                worksheet.Cells["E1"].Value = "Zi";
                worksheet.Cells["F1"].Value = "Ora Apel";
                worksheet.Cells["G1"].Value = "Durata Apel";
                worksheet.Cells["H1"].Value = "Problema";
                worksheet.Cells["I1"].Value = "Rezolvare";

                var headerCells = worksheet.Cells["A1:I1"];
                headerCells.Style.Font.Bold = true;
                headerCells.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                headerCells.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                headerCells.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);

                int row = 2;
                foreach (var entry in entries)
                {
                    worksheet.Cells[row, 1].Value = entry.FirmaNrTelefon.FirmaPunctLucru.Firma;
                    worksheet.Cells[row, 2].Value = entry.FirmaNrTelefon.FirmaPunctLucru.PctLucru;
                    worksheet.Cells[row, 3].Value = entry.FirmaNrTelefon.NrTelefon;
                    worksheet.Cells[row, 4].Value = entry.Data.ToString("yyyy-MM-dd");
                    worksheet.Cells[row, 5].Value = entry.Zi;
                    worksheet.Cells[row, 6].Value = entry.OraApel.ToString(@"hh\:mm\:ss");
                    worksheet.Cells[row, 7].Value = TimeSpan.TryParse(entry.DurataApel, out var duration) ? duration.ToString(@"hh\:mm\:ss") : entry.DurataApel;
                    worksheet.Cells[row, 8].Value = entry.Problema;
                    worksheet.Cells[row, 9].Value = entry.Rezolvare;
                    row++;
                }

                var dataCells = worksheet.Cells[$"A1:I{row - 1}"];
                dataCells.Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                dataCells.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                dataCells.Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                dataCells.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;

                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                var stream = new MemoryStream();
                package.SaveAs(stream);

                var content = stream.ToArray();
                _logger.LogInformation("ExportToExcel method succeeded: {Count} entries exported.", entries.Count);
                return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Entries.xlsx");
            }
        }


        [HttpGet]
        public async Task<IActionResult> ExportToPdf(string filterFirma, string filterPctLucru, string filterNrTelefon, DateTime? startDate, DateTime? endDate)
        {
            _logger.LogInformation("ExportToPdf method called with parameters: filterFirma={FilterFirma}, filterPctLucru={FilterPctLucru}, filterNrTelefon={FilterNrTelefon}, startDate={StartDate}, endDate={EndDate}", filterFirma, filterPctLucru, filterNrTelefon, startDate, endDate);

            var query = _context.HelpdeskEntries
                .Include(e => e.FirmaNrTelefon)
                .ThenInclude(f => f.FirmaPunctLucru)
                .AsQueryable();

            if (!string.IsNullOrEmpty(filterFirma))
            {
                query = query.Where(e => e.FirmaNrTelefon.FirmaPunctLucru.Firma.Contains(filterFirma));
            }

            if (!string.IsNullOrEmpty(filterPctLucru))
            {
                query = query.Where(e => e.FirmaNrTelefon.FirmaPunctLucru.PctLucru.Contains(filterPctLucru));
            }

            if (!string.IsNullOrEmpty(filterNrTelefon))
            {
                query = query.Where(e => e.FirmaNrTelefon.NrTelefon.Contains(filterNrTelefon));
            }

            if (startDate.HasValue)
            {
                query = query.Where(e => e.Data >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(e => e.Data <= endDate.Value);
            }

            query = query.OrderByDescending(e => e.Data).ThenByDescending(e => e.OraApel);

            var entries = await query.ToListAsync();

            using (var stream = new MemoryStream())
            {
                var document = new Document(iTextSharp.text.PageSize.A4.Rotate());
                PdfWriter.GetInstance(document, stream);
                document.Open();

                var table = new PdfPTable(9);
                table.WidthPercentage = 100;
                table.SetWidths(new float[] { 20f, 20f, 20f, 15f, 10f, 15f, 15f, 25f, 25f });

                var font = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.WHITE);

                PdfPCell cell = new PdfPCell(new Phrase("Firma", font));
                cell.BackgroundColor = BaseColor.GRAY;
                cell.NoWrap = true;
                table.AddCell(cell);

                cell = new PdfPCell(new Phrase("Punct de Lucru", font));
                cell.BackgroundColor = BaseColor.GRAY;
                cell.NoWrap = true;
                table.AddCell(cell);

                cell = new PdfPCell(new Phrase("Numar Telefon", font));
                cell.BackgroundColor = BaseColor.GRAY;
                cell.NoWrap = true;
                table.AddCell(cell);

                cell = new PdfPCell(new Phrase("Data", font));
                cell.BackgroundColor = BaseColor.GRAY;
                cell.NoWrap = true;
                table.AddCell(cell);

                cell = new PdfPCell(new Phrase("Zi", font));
                cell.BackgroundColor = BaseColor.GRAY;
                cell.NoWrap = true;
                table.AddCell(cell);

                cell = new PdfPCell(new Phrase("Ora Apel", font));
                cell.BackgroundColor = BaseColor.GRAY;
                cell.NoWrap = true;
                table.AddCell(cell);

                cell = new PdfPCell(new Phrase("Durata Apel", font));
                cell.BackgroundColor = BaseColor.GRAY;
                cell.NoWrap = true;
                table.AddCell(cell);

                cell = new PdfPCell(new Phrase("Problema", font));
                cell.BackgroundColor = BaseColor.GRAY;
                cell.NoWrap = true;
                table.AddCell(cell);

                cell = new PdfPCell(new Phrase("Rezolvare", font));
                cell.BackgroundColor = BaseColor.GRAY;
                cell.NoWrap = true;
                table.AddCell(cell);

                foreach (var entry in entries)
                {
                    table.AddCell(new PdfPCell(new Phrase(entry.FirmaNrTelefon.FirmaPunctLucru.Firma)) { NoWrap = false }); // Allow wrapping
                    table.AddCell(new PdfPCell(new Phrase(entry.FirmaNrTelefon.FirmaPunctLucru.PctLucru)) { NoWrap = false }); // Allow wrapping
                    table.AddCell(new PdfPCell(new Phrase(entry.FirmaNrTelefon.NrTelefon)) { NoWrap = false }); // Allow wrapping
                    table.AddCell(new PdfPCell(new Phrase(entry.Data.ToString("yyyy-MM-dd"))) { NoWrap = false }); // Allow wrapping
                    table.AddCell(new PdfPCell(new Phrase(entry.Zi)) { NoWrap = false }); // Allow wrapping
                    table.AddCell(new PdfPCell(new Phrase(entry.OraApel.ToString(@"hh\:mm\:ss"))) { NoWrap = false }); // Allow wrapping
                    table.AddCell(new PdfPCell(new Phrase(TimeSpan.TryParse(entry.DurataApel, out var duration) ? duration.ToString(@"hh\:mm\:ss") : entry.DurataApel)) { NoWrap = false }); // Allow wrapping
                    table.AddCell(new PdfPCell(new Phrase(entry.Problema)) { NoWrap = false }); // Allow wrapping
                    table.AddCell(new PdfPCell(new Phrase(entry.Rezolvare)) { NoWrap = false }); // Allow wrapping
                }

                document.Add(table);
                document.Close();

                var content = stream.ToArray();
                _logger.LogInformation("ExportToPdf method succeeded: {Count} entries exported.", entries.Count);
                return File(content, "application/pdf", "Entries.pdf");
            }
        }


        public async Task<IActionResult> Dashboard()
        {
            _logger.LogInformation("Dashboard method called.");
            var currentUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Nume == HttpContext.Session.GetString("Username"));

            if (currentUser == null)
            {
                _logger.LogWarning("Current user not found in session.");
                return Unauthorized();
            }

            ViewBag.UserName = currentUser?.Name;

            var currentMonth = DateTime.Now.Month;
            var currentYear = DateTime.Now.Year;

            var entries = await _context.HelpdeskEntries
                .Include(e => e.FirmaNrTelefon)
                .ThenInclude(f => f.FirmaPunctLucru)
                .ToListAsync();

            var daysInMonth = DateTime.DaysInMonth(currentYear, currentMonth);
            var callsData = Enumerable.Range(1, daysInMonth)
                .Select(day => new
                {
                    Date = new DateTime(currentYear, currentMonth, day),
                    Count = entries.Count(e => e.Data.Day == day && e.Data.Month == currentMonth && e.Data.Year == currentYear),
                    TotalDuration = entries.Where(e => e.Data.Day == day && e.Data.Month == currentMonth && e.Data.Year == currentYear)
                                           .Sum(e => TimeSpan.TryParse(e.DurataApel, out var duration) ? duration.TotalMinutes : 0)
                })
                .ToList();

            ViewBag.CallsData = new
            {
                labels = callsData.Select(d => d.Date.ToString("dd")).ToArray(),
                fullDates = callsData.Select(d => d.Date.ToString("yyyy-MM-dd")).ToArray(),
                data = callsData.Select(d => (double)d.Count).ToArray()
            };

            ViewBag.DurationsData = new
            {
                labels = callsData.Select(d => d.Date.ToString("dd")).ToArray(),
                fullDates = callsData.Select(d => d.Date.ToString("yyyy-MM-dd")).ToArray(),
                data = callsData.Select(d => Math.Round(d.TotalDuration, 2)).ToArray()
            };

            var allTimeTopFirms = entries
                .GroupBy(e => e.FirmaNrTelefon.FirmaPunctLucru.Firma)
                .Select(g => new
                {
                    Firma = g.Key,
                    TotalCalls = g.Count(),
                    TotalDuration = Math.Round(g.Sum(e => TimeSpan.TryParse(e.DurataApel, out var duration) ? duration.TotalMinutes : 0), 2)
                })
                .OrderByDescending(g => g.TotalCalls)
                .Take(10)
                .ToList();

            ViewBag.AllTimeTopFirms = allTimeTopFirms;

            var currentMonthTopFirms = entries
                .Where(e => e.Data.Month == currentMonth && e.Data.Year == currentYear)
                .GroupBy(e => e.FirmaNrTelefon.FirmaPunctLucru.Firma)
                .Select(g => new
                {
                    Firma = g.Key,
                    TotalCalls = g.Count(),
                    TotalDuration = Math.Round(g.Sum(e => TimeSpan.TryParse(e.DurataApel, out var duration) ? duration.TotalMinutes : 0), 2)
                })
                .OrderByDescending(g => g.TotalCalls)
                .Take(10)
                .ToList();

            ViewBag.CurrentMonthTopFirms = currentMonthTopFirms;

            var totalCallsCurrentMonth = entries.Count(e => e.Data.Month == currentMonth && e.Data.Year == currentYear);
            var totalDurationCurrentMonth = entries
                .Where(e => e.Data.Month == currentMonth && e.Data.Year == currentYear)
                .Sum(e => TimeSpan.TryParse(e.DurataApel, out var duration) ? duration.TotalMinutes : 0);

            var scheduleStart = new TimeSpan(8, 0, 0);
            var scheduleEnd = new TimeSpan(19, 0, 0);

            var totalCallsDuringSchedule = entries.Count(e =>
                e.Data.Month == currentMonth && e.Data.Year == currentYear &&
                e.OraApel >= scheduleStart && e.OraApel < scheduleEnd &&
                e.Data.DayOfWeek != DayOfWeek.Saturday && e.Data.DayOfWeek != DayOfWeek.Sunday);

            var totalDurationDuringSchedule = entries
                .Where(e =>
                    e.Data.Month == currentMonth && e.Data.Year == currentYear &&
                    e.OraApel >= scheduleStart && e.OraApel < scheduleEnd &&
                    e.Data.DayOfWeek != DayOfWeek.Saturday && e.Data.DayOfWeek != DayOfWeek.Sunday)
                .Sum(e => TimeSpan.TryParse(e.DurataApel, out var duration) ? duration.TotalMinutes : 0);

            var totalCallsOutsideSchedule = entries.Count(e =>
                e.Data.Month == currentMonth && e.Data.Year == currentYear &&
                (e.OraApel < scheduleStart || e.OraApel >= scheduleEnd ||
                 e.Data.DayOfWeek == DayOfWeek.Saturday || e.Data.DayOfWeek == DayOfWeek.Sunday));

            var totalDurationOutsideSchedule = entries
                .Where(e =>
                    e.Data.Month == currentMonth && e.Data.Year == currentYear &&
                    (e.OraApel < scheduleStart || e.OraApel >= scheduleEnd ||
                     e.Data.DayOfWeek == DayOfWeek.Saturday || e.Data.DayOfWeek == DayOfWeek.Sunday))
                .Sum(e => TimeSpan.TryParse(e.DurataApel, out var duration) ? duration.TotalMinutes : 0);

            ViewBag.TotalCallsCurrentMonth = totalCallsCurrentMonth;
            ViewBag.TotalDurationCurrentMonthMinutes = totalDurationCurrentMonth.ToString("F2");
            ViewBag.TotalDurationCurrentMonthHours = (totalDurationCurrentMonth / 60).ToString("F2");

            ViewBag.TotalCallsDuringSchedule = totalCallsDuringSchedule;
            ViewBag.TotalDurationDuringScheduleMinutes = totalDurationDuringSchedule.ToString("F2");
            ViewBag.TotalDurationDuringScheduleHours = (totalDurationDuringSchedule / 60).ToString("F2");

            ViewBag.TotalCallsOutsideSchedule = totalCallsOutsideSchedule;
            ViewBag.TotalDurationOutsideScheduleMinutes = totalDurationOutsideSchedule.ToString("F2");
            ViewBag.TotalDurationOutsideScheduleHours = (totalDurationOutsideSchedule / 60).ToString("F2");

            var firmsWithoutIdCount = await _context.FirmaPunctLucruEntries
                .Where(f => !_context.FirmaNrTelefonEntries.Any(nr => nr.ID_firma_punct_lucru == f.Id))
                .CountAsync();

            var firmsWithMultipleIdsCount = await _context.FirmaNrTelefonEntries
                .GroupBy(nr => nr.ID_firma_punct_lucru)
                .Where(g => g.Count() > 1)
                .CountAsync();

            ViewBag.FirmsWithoutIdCount = firmsWithoutIdCount;
            ViewBag.FirmsWithMultipleIdsCount = firmsWithMultipleIdsCount;

            _logger.LogInformation("Dashboard method succeeded: Data retrieved.");
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetSingleSuggestion(string firma)
        {
            _logger.LogInformation("GetSingleSuggestion method called with parameter: firma={Firma}", firma);

            if (string.IsNullOrWhiteSpace(firma))
            {
                _logger.LogWarning("GetSingleSuggestion method failed: Invalid input.");
                return BadRequest("Invalid input");
            }

            var firmaPunctLucruEntries = await _context.FirmaPunctLucruEntries
                .Where(f => f.Firma == firma)
                .ToListAsync();

            if (firmaPunctLucruEntries.Count == 1)
            {
                var punctLucru = firmaPunctLucruEntries.First();
                var nrTelefonEntries = await _context.FirmaNrTelefonEntries
                    .Where(f => f.ID_firma_punct_lucru == punctLucru.Id)
                    .ToListAsync();

                var nrTelefon = nrTelefonEntries.FirstOrDefault()?.NrTelefon ?? "";

                _logger.LogInformation("GetSingleSuggestion method succeeded: Single suggestion found for firma {Firma}", firma);
                return Json(new { pctLucru = punctLucru.PctLucru, nrTelefon });
            }

            _logger.LogInformation("GetSingleSuggestion method succeeded: No single suggestion found for firma {Firma}", firma);
            return Json(new { pctLucru = "", nrTelefon = "" });
        }

        public async Task<IActionResult> ListaNumereTelefon(string filterFirma = "", string filterPctLucru = "", string filterNrTelefon = "", int page = 1)
        {
            _logger.LogInformation("ListaNumereTelefon method called with parameters: filterFirma={FilterFirma}, filterPctLucru={FilterPctLucru}, filterNrTelefon={FilterNrTelefon}, page={Page}", filterFirma, filterPctLucru, filterNrTelefon, page);

            var currentUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Nume == HttpContext.Session.GetString("Username"));

            if (currentUser == null)
            {
                _logger.LogWarning("ListaNumereTelefon method failed: Current user not found in session.");
                return Unauthorized();
            }

            ViewBag.UserName = currentUser?.Name;

            var query = _context.FirmaNrTelefonEntries
                .Include(fnt => fnt.FirmaPunctLucru)
                .AsQueryable();

            if (!string.IsNullOrEmpty(filterFirma))
            {
                query = query.Where(fnt => fnt.FirmaPunctLucru.Firma.Contains(filterFirma));
            }

            if (!string.IsNullOrEmpty(filterPctLucru))
            {
                query = query.Where(fnt => fnt.FirmaPunctLucru.PctLucru.Contains(filterPctLucru));
            }

            if (!string.IsNullOrEmpty(filterNrTelefon))
            {
                query = query.Where(fnt => fnt.NrTelefon.Contains(filterNrTelefon));
            }

            var totalEntries = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalEntries / (double)PageSize);

            var entries = await query
                .OrderBy(fnt => fnt.FirmaPunctLucru.Firma)
                .ThenBy(fnt => fnt.FirmaPunctLucru.PctLucru)
                .ThenBy(fnt => fnt.NrTelefon)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(fnt => new
                {
                    fnt.Id,  // Include the Id in the anonymous type
                    fnt.FirmaPunctLucru.Firma,
                    fnt.FirmaPunctLucru.PctLucru,
                    fnt.NrTelefon
                })
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.FilterFirma = filterFirma;
            ViewBag.FilterPctLucru = filterPctLucru;
            ViewBag.FilterNrTelefon = filterNrTelefon;

            _logger.LogInformation("ListaNumereTelefon method succeeded: Retrieved {TotalEntries} entries.", totalEntries);
            return View(entries);
        }

        [HttpPost]
        public async Task<IActionResult> ExportFilteredEntriesToExcel([FromBody] ExportDataRequest request)
        {
            _logger.LogInformation("ExportFilteredEntriesToExcel method called with request: {Request}", request);

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Numere Telefon");

                // Style the header
                var headerCells = worksheet.Cells["A1:C1"];
                headerCells.Style.Font.Bold = true;
                headerCells.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                headerCells.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                headerCells.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);

                worksheet.Cells["A1"].Value = "Firma";
                worksheet.Cells["B1"].Value = "Punct de Lucru";
                worksheet.Cells["C1"].Value = "Numar Telefon";

                int row = 2;
                foreach (var entry in request.Rows)
                {
                    worksheet.Cells[row, 1].Value = entry.Firma;
                    worksheet.Cells[row, 2].Value = entry.PctLucru;
                    worksheet.Cells[row, 3].Value = entry.NrTelefon;
                    row++;
                }

                var dataCells = worksheet.Cells[$"A1:C{row - 1}"];
                dataCells.Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                dataCells.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                dataCells.Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                dataCells.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;

                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns(20);

                var stream = new MemoryStream();
                package.SaveAs(stream);

                var content = stream.ToArray();
                _logger.LogInformation("ExportFilteredEntriesToExcel method succeeded: {RowCount} rows exported.", request.Rows.Count);
                return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "FilteredEntries.xlsx");
            }
        }

        public async Task<IActionResult> ListaFirmaPunctLucru(string filterFirma = "", string filterPctLucru = "", int page = 1)
        {
            _logger.LogInformation("ListaFirmaPunctLucru method called with parameters: filterFirma={FilterFirma}, filterPctLucru={FilterPctLucru}, page={Page}", filterFirma, filterPctLucru, page);

            var currentUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Nume == HttpContext.Session.GetString("Username"));

            if (currentUser == null)
            {
                _logger.LogWarning("ListaFirmaPunctLucru method failed: Current user not found in session.");
                return Unauthorized();
            }

            ViewBag.UserName = currentUser?.Name;

            var query = _context.FirmaPunctLucruEntries.AsQueryable();

            if (!string.IsNullOrEmpty(filterFirma))
            {
                query = query.Where(e => e.Firma.Contains(filterFirma));
            }

            if (!string.IsNullOrEmpty(filterPctLucru))
            {
                query = query.Where(e => e.PctLucru.Contains(filterPctLucru));
            }

            var totalEntries = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalEntries / (double)PageSize);

            var entries = await query
                .OrderBy(e => e.Firma)
                .ThenBy(e => e.PctLucru)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(e => new
                {
                    e.Firma,
                    e.PctLucru
                })
                .Distinct()
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.FilterFirma = filterFirma;
            ViewBag.FilterPctLucru = filterPctLucru;

            _logger.LogInformation("ListaFirmaPunctLucru method succeeded: Retrieved {TotalEntries} entries.", totalEntries);
            return View(entries);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteFirmaNrTelefon(int id)
        {
            _logger.LogInformation("DeleteFirmaNrTelefon method called with ID: {Id}", id);

            var nrTelefonEntry = await _context.FirmaNrTelefonEntries
                .FirstOrDefaultAsync(e => e.Id == id);

            if (nrTelefonEntry == null)
            {
                _logger.LogWarning("DeleteFirmaNrTelefon method failed: Numarul de telefon not found with ID {Id}", id);
                return Json(new { success = false, message = "Numarul de telefon nu a fost gasit." });
            }

            var hasAssociatedEntries = await _context.HelpdeskEntries
                .AnyAsync(e => e.ID_nr_telefon == id);

            if (hasAssociatedEntries)
            {
                _logger.LogWarning("DeleteFirmaNrTelefon method failed: Numarul de telefon is already associated with Helpdesk entries.");
                return Json(new { success = false, message = "Numarul de telefon este deja asociat." });
            }

            _context.FirmaNrTelefonEntries.Remove(nrTelefonEntry);
            await _context.SaveChangesAsync();

            _logger.LogInformation("DeleteFirmaNrTelefon method succeeded: Numarul de telefon with ID {Id} deleted.", id);
            return Json(new { success = true });
        }

        public IActionResult InputRaport()
        {
            _logger.LogInformation("InputRaport method called.");
            var currentUser = _context.Users
                .FirstOrDefault(u => u.Nume == HttpContext.Session.GetString("Username"));

            if (currentUser == null)
            {
                _logger.LogWarning("InputRaport method failed: Current user not found in session.");
                return Unauthorized();
            }

            ViewBag.UserName = currentUser?.Name;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GenerateReport(DateTime startDate, DateTime endDate)
        {
            _logger.LogInformation("GenerateReport method called with parameters: startDate={StartDate}, endDate={EndDate}", startDate, endDate);

            var currentUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Nume == HttpContext.Session.GetString("Username"));

            if (currentUser == null)
            {
                _logger.LogWarning("GenerateReport method failed: Current user not found in session.");
                return Unauthorized();
            }

            ViewBag.UserName = currentUser?.Name;
            ViewBag.StartDate = startDate.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate.ToString("yyyy-MM-dd");

            var entries = await _context.HelpdeskEntries
                .Include(e => e.FirmaNrTelefon)
                .ThenInclude(f => f.FirmaPunctLucru)
                .Where(e => e.Data >= startDate && e.Data <= endDate)
                .ToListAsync();

            var totalCalls = entries.Count;
            var totalDurationMinutes = entries
                .Sum(e => TimeSpan.TryParse(e.DurataApel, out var duration) ? duration.TotalMinutes : 0);

            var scheduleStart = new TimeSpan(8, 0, 0);
            var scheduleEnd = new TimeSpan(19, 0, 0);

            var totalCallsDuringSchedule = entries.Count(e =>
                (e.OraApel >= scheduleStart && e.OraApel < scheduleEnd) &&
                e.Data.DayOfWeek != DayOfWeek.Saturday && e.Data.DayOfWeek != DayOfWeek.Sunday);

            var totalDurationDuringSchedule = entries
                .Where(e =>
                    (e.OraApel >= scheduleStart && e.OraApel < scheduleEnd) &&
                    e.Data.DayOfWeek != DayOfWeek.Saturday && e.Data.DayOfWeek != DayOfWeek.Sunday)
                .Sum(e => TimeSpan.TryParse(e.DurataApel, out var duration) ? duration.TotalMinutes : 0);

            var totalCallsOutsideSchedule = entries.Count(e =>
                (e.OraApel < scheduleStart || e.OraApel >= scheduleEnd) ||
                e.Data.DayOfWeek == DayOfWeek.Saturday || e.Data.DayOfWeek == DayOfWeek.Sunday);

            var totalDurationOutsideSchedule = entries
                .Where(e =>
                    (e.OraApel < scheduleStart || e.OraApel >= scheduleEnd) ||
                    e.Data.DayOfWeek == DayOfWeek.Saturday || e.Data.DayOfWeek == DayOfWeek.Sunday)
                .Sum(e => TimeSpan.TryParse(e.DurataApel, out var duration) ? duration.TotalMinutes : 0);

            ViewBag.TotalCallsCurrentMonth = totalCalls;
            ViewBag.TotalDurationCurrentMonthMinutes = totalDurationMinutes.ToString("F2");
            ViewBag.TotalDurationCurrentMonthHours = (totalDurationMinutes / 60).ToString("F2");

            ViewBag.TotalCallsDuringSchedule = totalCallsDuringSchedule;
            ViewBag.TotalDurationDuringScheduleMinutes = totalDurationDuringSchedule.ToString("F2");
            ViewBag.TotalDurationDuringScheduleHours = (totalDurationDuringSchedule / 60).ToString("F2");

            ViewBag.TotalCallsOutsideSchedule = totalCallsOutsideSchedule;
            ViewBag.TotalDurationOutsideScheduleMinutes = totalDurationOutsideSchedule.ToString("F2");
            ViewBag.TotalDurationOutsideScheduleHours = (totalDurationOutsideSchedule / 60).ToString("F2");

            var totalPerFirma = entries
                .GroupBy(e => e.FirmaNrTelefon.FirmaPunctLucru.Firma)
                .Select(g => new
                {
                    Firma = g.Key,
                    TotalCalls = g.Count(),
                    TotalDuration = Math.Round(g.Sum(e => TimeSpan.TryParse(e.DurataApel, out var duration) ? duration.TotalMinutes : 0), 2)
                })
                .OrderByDescending(g => g.TotalCalls)
                .ToList();

            ViewBag.TotalPerFirma = totalPerFirma;

            var daysInPeriod = (endDate - startDate).Days + 1;
            var callsData = Enumerable.Range(0, daysInPeriod)
                .Select(day => new
                {
                    Date = startDate.AddDays(day),
                    Count = entries.Count(e => e.Data == startDate.AddDays(day)),
                    TotalDuration = entries.Where(e => e.Data == startDate.AddDays(day))
                                           .Sum(e => TimeSpan.TryParse(e.DurataApel, out var duration) ? duration.TotalMinutes : 0)
                })
                .ToList();

            ViewBag.CallsData = new
            {
                labels = callsData.Select(d => d.Date.ToString("dd")).ToArray(),
                fullDates = callsData.Select(d => d.Date.ToString("yyyy-MM-dd")).ToArray(),
                data = callsData.Select(d => (double)d.Count).ToArray()
            };

            ViewBag.DurationsData = new
            {
                labels = callsData.Select(d => d.Date.ToString("dd")).ToArray(),
                fullDates = callsData.Select(d => d.Date.ToString("yyyy-MM-dd")).ToArray(),
                data = callsData.Select(d => Math.Round(d.TotalDuration, 2)).ToArray()
            };

            _logger.LogInformation("GenerateReport method succeeded: Report generated.");
            return View("Report", entries);
        }

        public async Task<IActionResult> ListaFirma(string filterFirma = "", int? filterPriority = null)
        {
            _logger.LogInformation("ListaFirma method called with parameters: filterFirma={FilterFirma}, filterPriority={FilterPriority}", filterFirma, filterPriority);

            var currentUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Nume == HttpContext.Session.GetString("Username"));

            if (currentUser == null)
            {
                _logger.LogWarning("ListaFirma method failed: Current user not found in session.");
                return Unauthorized();
            }

            ViewBag.UserName = currentUser?.Name;

            var query = _context.FirmaPunctLucruEntries.AsQueryable();

            if (!string.IsNullOrEmpty(filterFirma))
            {
                query = query.Where(f => f.Firma.Contains(filterFirma));
            }

            if (filterPriority.HasValue)
            {
                query = query.Where(f => f.Priority == filterPriority.Value);
            }

            var firms = await query
                .Where(f => f.Priority != null && f.Priority != 0)
                .GroupBy(f => f.Firma)
                .Select(g => new FirmaPunctLucru
                {
                    Firma = g.Key,
                    Priority = g.First().Priority ?? 0
                })
                .ToListAsync();

            if (!string.IsNullOrEmpty(filterFirma) && !filterPriority.HasValue)
            {
                var filteredFirms = await _context.FirmaPunctLucruEntries
                    .Where(f => f.Firma.Contains(filterFirma) && f.Priority != null && f.Priority != 0)
                    .GroupBy(f => f.Priority)
                    .Select(g => new
                    {
                        Priority = g.Key,
                        Firms = g.Select(f => f.Firma).Distinct().ToList()
                    })
                    .ToDictionaryAsync(g => (int?)g.Priority, g => g.Firms);

                if (filteredFirms.Any())
                {
                    ViewBag.FilterFirma = filterFirma;
                    ViewBag.FilterPriority = filterPriority;
                    return View(filteredFirms);
                }
            }

            var firmsByPriority = firms
                .GroupBy(f => (int?)f.Priority)
                .ToDictionary(g => g.Key, g => g.Select(f => f.Firma).ToList());

            ViewBag.FilterFirma = filterFirma;
            ViewBag.FilterPriority = filterPriority;

            _logger.LogInformation("ListaFirma method succeeded: Retrieved firms.");
            return View(firmsByPriority);
        }

        public class ExportDataRequest
        {
            public List<ExportEntry> Rows { get; set; }
        }

        public class ExportEntry
        {
            public string Firma { get; set; }
            public string PctLucru { get; set; }
            public string NrTelefon { get; set; }
        }
    }

    public class UpdateFieldRequest
    {
        public long Id { get; set; }
        public Dictionary<string, string> Fields { get; set; }
    }
}
