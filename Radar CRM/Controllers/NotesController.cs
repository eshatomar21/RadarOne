using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Radar_CRM.Data;
using Radar_CRM.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Radar_CRM.Controllers
{
    public class NotesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NotesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // GET: Notes/Index (Paginated, Sorted, Filtered)
        // ==========================================
        public async Task<IActionResult> Index(int page = 1, string search = "", string sortCol = "Id", string sortDir = "desc", string advancedFilters = "")
        {
            if (!User.Identity.IsAuthenticated) return RedirectToAction("Login", "Users");

            int pageSize = 100;

            var query = _context.Note
                .Include(n => n.NoteOwner)
                .AsQueryable();

            // 🚀 BULLETPROOF FILTERING ENGINE WITH AND/OR SUPPORT
            if (!string.IsNullOrWhiteSpace(advancedFilters))
            {
                try
                {
                    string jsonString = advancedFilters;
                    if (jsonString.Contains("%5B") || jsonString.Contains("%7B"))
                        jsonString = Uri.UnescapeDataString(jsonString);

                    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var filters = System.Text.Json.JsonSerializer.Deserialize<List<NoteFilterCriteria>>(jsonString, options);

                    if (filters != null && filters.Any())
                    {
                        var parameter = System.Linq.Expressions.Expression.Parameter(typeof(Notes), "n");
                        System.Linq.Expressions.Expression combinedPredicate = null;

                        foreach (var f in filters)
                        {
                            if (string.IsNullOrWhiteSpace(f.Value) && f.Condition != "is_empty" && f.Condition != "is_not_empty") continue;

                            var propertyInfo = typeof(Notes).GetProperty(f.ColumnName, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            if (propertyInfo == null) continue;

                            var propExpr = System.Linq.Expressions.Expression.Property(parameter, propertyInfo);
                            System.Linq.Expressions.Expression conditionExpr = null;

                            if (propertyInfo.PropertyType == typeof(string))
                            {
                                var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes);
                                var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) });
                                var startsWithMethod = typeof(string).GetMethod("StartsWith", new[] { typeof(string) });
                                var endsWithMethod = typeof(string).GetMethod("EndsWith", new[] { typeof(string) });

                                var notNullProp = System.Linq.Expressions.Expression.Coalesce(propExpr, System.Linq.Expressions.Expression.Constant(""));
                                var lowerProp = System.Linq.Expressions.Expression.Call(notNullProp, toLowerMethod);
                                var lowerVal = System.Linq.Expressions.Expression.Constant(f.Value?.ToLower().Trim() ?? "");

                                if (f.Condition == "is") conditionExpr = System.Linq.Expressions.Expression.Equal(lowerProp, lowerVal);
                                else if (f.Condition == "is_not") conditionExpr = System.Linq.Expressions.Expression.NotEqual(lowerProp, lowerVal);
                                else if (f.Condition == "contains") conditionExpr = System.Linq.Expressions.Expression.Call(lowerProp, containsMethod, lowerVal);
                                else if (f.Condition == "does_not_contain") conditionExpr = System.Linq.Expressions.Expression.Not(System.Linq.Expressions.Expression.Call(lowerProp, containsMethod, lowerVal));
                                else if (f.Condition == "starts_with") conditionExpr = System.Linq.Expressions.Expression.Call(lowerProp, startsWithMethod, lowerVal);
                                else if (f.Condition == "ends_with") conditionExpr = System.Linq.Expressions.Expression.Call(lowerProp, endsWithMethod, lowerVal);
                                else if (f.Condition == "is_empty") conditionExpr = System.Linq.Expressions.Expression.Call(typeof(string).GetMethod("IsNullOrEmpty"), propExpr);
                                else if (f.Condition == "is_not_empty") conditionExpr = System.Linq.Expressions.Expression.Not(System.Linq.Expressions.Expression.Call(typeof(string).GetMethod("IsNullOrEmpty"), propExpr));
                            }

                            if (conditionExpr != null)
                            {
                                if (combinedPredicate == null) combinedPredicate = conditionExpr;
                                else
                                {
                                    if (string.Equals(f.LogicalOperator, "OR", StringComparison.OrdinalIgnoreCase))
                                        combinedPredicate = System.Linq.Expressions.Expression.OrElse(combinedPredicate, conditionExpr);
                                    else
                                        combinedPredicate = System.Linq.Expressions.Expression.AndAlso(combinedPredicate, conditionExpr);
                                }
                            }
                        }

                        if (combinedPredicate != null)
                        {
                            var lambda = System.Linq.Expressions.Expression.Lambda<Func<Notes, bool>>(combinedPredicate, parameter);
                            query = query.Where(lambda);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("FILTER ENGINE ERROR: " + ex.Message);
                }
            }

            if (!string.IsNullOrEmpty(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(n =>
                    (n.NoteTitle != null && n.NoteTitle.ToLower().Contains(searchLower)) ||
                    (n.Description != null && n.Description.ToLower().Contains(searchLower)) ||
                    (n.NoteContent != null && n.NoteContent.ToLower().Contains(searchLower)) ||
                    (n.AccountName != null && n.AccountName.ToLower().Contains(searchLower)) ||
                    (n.LeadName != null && n.LeadName.ToLower().Contains(searchLower)) ||
                    (n.DealName != null && n.DealName.ToLower().Contains(searchLower))
                );
            }

            if (sortDir == "desc")
            {
                query = sortCol switch
                {
                    "CreatedDateTime" => query.OrderByDescending(n => n.CreatedDateTime),
                    "NoteTitle" => query.OrderByDescending(n => n.NoteTitle),
                    "AccountName" => query.OrderByDescending(n => n.AccountName),
                    _ => query.OrderByDescending(n => n.Id)
                };
            }
            else
            {
                query = sortCol switch
                {
                    "CreatedDateTime" => query.OrderBy(n => n.CreatedDateTime),
                    "NoteTitle" => query.OrderBy(n => n.NoteTitle),
                    "AccountName" => query.OrderBy(n => n.AccountName),
                    _ => query.OrderBy(n => n.Id)
                };
            }

            var totalRecords = await query.CountAsync();
            if (page < 1) page = 1;
            int totalPagesCalc = (int)Math.Ceiling(totalRecords / (double)pageSize);
            if (totalPagesCalc == 0) totalPagesCalc = 1;
            if (page > totalPagesCalc) page = totalPagesCalc;

            var notes = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPagesCalc;
            ViewBag.TotalRecords = totalRecords;

            return View(notes);
        }

        // ==========================================
        // BULK UPLOAD (MAPPED SECURELY TO newnotes.csv)
        // ==========================================
        [HttpPost]
        [DisableRequestSizeLimit] // Prevents 30MB upload limit errors
        [RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = int.MaxValue)]
        public async Task<IActionResult> UploadFile(IFormFile uploadedFile)
        {
            if (uploadedFile == null || uploadedFile.Length == 0) return BadRequest("No file was uploaded.");

            var notesToInsert = new List<Notes>();
            int currentRow = 1;
            int skippedCount = 0; // 🚀 Added to track skipped records

            // Load Maps for Polymorphic linking
            var accountLookup = _context.Accounts.Where(a => a.ZohoRecordId != null).ToDictionary(a => a.ZohoRecordId, a => a.Id);
            var leadLookup = _context.Leads.Where(l => l.ZohoRecordId != null).ToDictionary(l => l.ZohoRecordId, l => l.Id);
            var dealLookup = _context.Deals.Where(d => d.ZohoRecordId != null).ToDictionary(d => d.ZohoRecordId, d => d.Id);
            var taskLookup = _context.Tasks.Where(t => t.ZohoRecordId != null).ToDictionary(t => t.ZohoRecordId, t => t.Id);
            var validUserIds = new HashSet<string>(_context.Users.Select(u => u.Id));

            // 🚀 NEW: Load existing Notes ZohoRecordIds to skip duplicates ultra-fast
            var existingNotes = new HashSet<string>(
                await _context.Note
                    .Where(n => !string.IsNullOrEmpty(n.ZohoRecordId))
                    .Select(n => n.ZohoRecordId)
                    .ToListAsync()
            );

            try
            {
                using (var stream = uploadedFile.OpenReadStream())
                {
                    var allRows = ParseCsvRobust(stream); // Use your robust parser to handle line breaks in notes
                    if (allRows == null || allRows.Count == 0) return BadRequest("CSV file is empty or invalid.");

                    // Standardize headers
                    var headers = allRows[0].Select(h => h.Trim().ToLower().Replace(" ", "")).ToList();

                    string GetValSafe(string[] vals, string colName)
                    {
                        var idx = headers.IndexOf(colName.ToLower().Replace(" ", ""));
                        return idx >= 0 && idx < vals.Length ? vals[idx]?.Trim() ?? "" : "";
                    }

                    // Loop through data rows (skip header)
                    for (int i = 1; i < allRows.Count; i++)
                    {
                        currentRow++;
                        var values = allRows[i];

                        string zohoRecordId = GetValSafe(values, "RecordId");

                        // 🚀 SKIP LOGIC: If the note already exists in the database, skip it entirely
                        if (!string.IsNullOrEmpty(zohoRecordId) && existingNotes.Contains(zohoRecordId))
                        {
                            skippedCount++;
                            continue;
                        }

                        string parentZohoId = GetValSafe(values, "ParentID.id");
                        string parentName = GetValSafe(values, "ParentID"); // The raw text name (e.g. DrRachana Gupta)
                        string rawNoteContent = GetValSafe(values, "NoteContent");

                        // Fallback for required fields
                        if (string.IsNullOrWhiteSpace(rawNoteContent)) rawNoteContent = "No Content Provided";

                        var newNote = new Notes
                        {
                            ZohoRecordId = zohoRecordId,

                            // MAP THE NOTE TEXT
                            NoteTitle = GetValSafe(values, "NoteTitle"),
                            NoteContent = rawNoteContent,
                            Description = rawNoteContent, // Mapped to Description since it's [Required] in your model

                            // MAP THE AUDIT FIELDS
                            CreatedBy = GetValSafe(values, "CreatedBy"),
                            ModifiedBy = GetValSafe(values, "ModifiedBy"),
                            CreatedDateTime = DateTime.TryParse(GetValSafe(values, "CreatedTime"), out DateTime cDt) ? cDt : DateTime.Now,
                            ModifiedTime = DateTime.TryParse(GetValSafe(values, "ModifiedTime"), out DateTime mDt) ? mDt : null,

                            // MAP THE OWNER
                            NoteOwnerId = validUserIds.Contains(GetValSafe(values, "NoteOwner.id")) ? GetValSafe(values, "NoteOwner.id") : null,
                        };

                        // DYNAMIC POLYMORPHIC LINKING & NAME ASSIGNMENT
                        // It assigns the correct Foreign Key ID *and* the Parent Text Name
                        if (!string.IsNullOrEmpty(parentZohoId))
                        {
                            if (accountLookup.TryGetValue(parentZohoId, out int aId))
                            {
                                newNote.AccountId = aId;
                                newNote.AccountName = parentName;
                            }
                            else if (leadLookup.TryGetValue(parentZohoId, out int lId))
                            {
                                newNote.LeadId = lId;
                                newNote.LeadName = parentName;
                            }
                            else if (dealLookup.TryGetValue(parentZohoId, out int dId))
                            {
                                newNote.DealId = dId;
                                newNote.DealName = parentName;
                            }
                            else if (taskLookup.TryGetValue(parentZohoId, out int tId))
                            {
                                newNote.TaskId = tId;
                                newNote.TaskName = parentName;
                            }
                        }

                        // 🚀 Add to HashSet to prevent duplicate inserts if the CSV file itself contains duplicate rows
                        if (!string.IsNullOrEmpty(zohoRecordId))
                        {
                            existingNotes.Add(zohoRecordId);
                        }

                        notesToInsert.Add(newNote);
                    }
                }

                _context.ChangeTracker.AutoDetectChangesEnabled = false;
                await _context.Note.AddRangeAsync(notesToInsert);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                string trueError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, $"Failed at Row {currentRow} -> {trueError}");
            }
            finally
            {
                _context.ChangeTracker.AutoDetectChangesEnabled = true;
            }

            // 🚀 Added skippedCount to the response so the UI knows how many were ignored
            return Json(new { success = true, insertedCount = notesToInsert.Count, skippedCount = skippedCount });
        }

        // ==========================================
        // ROBUST MULTI-LINE CSV PARSER 
        // ==========================================
        private string GetVal(string[] values, int index)
        {
            if (index < values.Length) return values[index]?.Trim() ?? "";
            return "";
        }

        private List<string[]> ParseCsvRobust(Stream stream)
        {
            var rows = new List<string[]>();
            using var reader = new StreamReader(stream);

            bool inQuotes = false;
            var currentField = new System.Text.StringBuilder();
            var currentRow = new List<string>();

            while (!reader.EndOfStream)
            {
                string line = reader.ReadLine();
                if (line == null) break;

                for (int i = 0; i < line.Length; i++)
                {
                    char c = line[i];

                    if (c == '\"')
                    {
                        if (inQuotes && i + 1 < line.Length && line[i + 1] == '\"')
                        {
                            currentField.Append('\"');
                            i++; // Skip the escaped quote
                        }
                        else
                        {
                            inQuotes = !inQuotes;
                        }
                    }
                    else if (c == ',' && !inQuotes)
                    {
                        currentRow.Add(currentField.ToString());
                        currentField.Clear();
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                }

                if (inQuotes)
                {
                    // Field contains a line break, keep reading on the next loop
                    currentField.Append("\n");
                }
                else
                {
                    // Finished parsing row
                    currentRow.Add(currentField.ToString());
                    rows.Add(currentRow.ToArray());
                    currentRow.Clear();
                    currentField.Clear();
                }
            }
            return rows;
        }

        // ==========================================
        // HELPER CLASSES
        // ==========================================
        public class NoteFilterCriteria
        {
            public string LogicalOperator { get; set; }
            public string ColumnName { get; set; }
            public string Condition { get; set; }
            public string Value { get; set; }
            public bool IsDate { get; set; }
        }
    }
}