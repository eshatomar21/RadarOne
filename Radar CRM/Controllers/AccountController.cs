using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Radar_CRM.Data;
using Radar_CRM.Models;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Security.Claims;
using CrmTaskModel = Radar_CRM.Models.Task;
using Task = System.Threading.Tasks.Task;

namespace Radar_CRM.Controllers
{
    public class AccountsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public AccountsController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }



        // ==========================================
        // INDEX: GET (Hierarchical List View)
        // ==========================================
        public async Task<IActionResult> Index(int page = 1, string search = "", string sortCol = "CreatedDateAndTime", string sortDir = "desc", string advancedFilters = "")
        {
            if (!User.Identity.IsAuthenticated) return RedirectToAction("Login", "Users");

            string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentUser = await _context.Users.FindAsync(currentUserId);

            if (currentUser == null) return RedirectToAction("Login", "Users");

            int pageSize = 100;
            var query = _context.Accounts.AsQueryable();

            // 🚀 ADMIN CHECK BASED ON 'PROFILE'
            bool isAdmin = !string.IsNullOrWhiteSpace(currentUser.Profile) &&
                           (currentUser.Profile.Contains("Admin", StringComparison.OrdinalIgnoreCase) ||
                            currentUser.Profile.Equals("Administrator", StringComparison.OrdinalIgnoreCase));

            if (!isAdmin)
            {
                var allRoles = await _context.Roles.ToListAsync();
                var visibleRoleIds = new List<int>();

                var subordinateIds = GetSubordinateRoleIds(allRoles, currentUser.RoleId);
                visibleRoleIds.AddRange(subordinateIds);

                var currentUserRoleModel = allRoles.FirstOrDefault(r => r.Id == currentUser.RoleId);
                if (currentUserRoleModel != null && currentUserRoleModel.ShareDataWithPeers && currentUser.RoleId.HasValue)
                {
                    visibleRoleIds.Add(currentUser.RoleId.Value);
                }

                var visibleUserIds = await _context.Users
                    .Where(u => u.RoleId.HasValue && visibleRoleIds.Contains(u.RoleId.Value))
                    .Select(u => u.Id)
                    .ToListAsync();

                visibleUserIds.Add(currentUserId);
                query = query.Where(a => visibleUserIds.Contains(a.AccountOwnerId));
            }

            // 🚀 BULLETPROOF FILTERING ENGINE
            if (!string.IsNullOrWhiteSpace(advancedFilters))
            {
                try
                {
                    string jsonString = advancedFilters;
                    if (jsonString.Contains("%5B") || jsonString.Contains("%7B"))
                    {
                        jsonString = Uri.UnescapeDataString(jsonString);
                    }

                    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var filters = System.Text.Json.JsonSerializer.Deserialize<List<FilterCriteria>>(jsonString, options);

                    if (filters != null && filters.Any())
                    {
                        // 1. 🚀 INTERCEPT RELATED TASK FILTERS
                        var taskFilters = filters.Where(f => f.ColumnName == "RelatedTasksPresence" || f.ColumnName.StartsWith("Task.")).ToList();

                        // Remove them from the main list so the Account-reflection engine ignores them
                        filters.RemoveAll(f => taskFilters.Contains(f));

                        // =======================================================
                        // 2. PROCESS STANDARD ACCOUNT FILTERS
                        // =======================================================
                        var parameter = System.Linq.Expressions.Expression.Parameter(typeof(Account), "a");
                        System.Linq.Expressions.Expression combinedPredicate = null;

                        foreach (var f in filters)
                        {
                            if (string.IsNullOrWhiteSpace(f.Value) && f.Condition != "is_empty" && f.Condition != "is_not_empty") continue;

                            string dbColName = f.ColumnName;

                            var propertyInfo = typeof(Account).GetProperty(dbColName, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            if (propertyInfo != null && propertyInfo.PropertyType.IsClass && propertyInfo.PropertyType != typeof(string))
                            {
                                var idProp = typeof(Account).GetProperty(dbColName + "Id", System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                if (idProp != null)
                                {
                                    dbColName = idProp.Name;
                                    propertyInfo = idProp;
                                }
                            }

                            if (propertyInfo == null) continue;

                            var propExpr = System.Linq.Expressions.Expression.Property(parameter, propertyInfo);
                            System.Linq.Expressions.Expression conditionExpr = null;
                            string safeValue = f.Value?.Replace("+", " ").Trim() ?? "";

                            // DATES
                            if (f.IsDate || propertyInfo.PropertyType == typeof(DateTime) || propertyInfo.PropertyType == typeof(DateTime?))
                            {
                                string[] dates = safeValue.Split('|');
                                if (DateTime.TryParse(dates[0], out DateTime dVal))
                                {
                                    DateTime dVal2 = dates.Length > 1 && DateTime.TryParse(dates[1], out var d2) ? d2 : dVal;

                                    var startOfDay = dVal.Date;
                                    var endOfDay = dVal.Date.AddDays(1).AddTicks(-1);
                                    var endOfDay2 = dVal2.Date.AddDays(1).AddTicks(-1);

                                    var constStart = System.Linq.Expressions.Expression.Constant(startOfDay, propertyInfo.PropertyType);
                                    var constEnd = System.Linq.Expressions.Expression.Constant(endOfDay, propertyInfo.PropertyType);
                                    var constEnd2 = System.Linq.Expressions.Expression.Constant(endOfDay2, propertyInfo.PropertyType);

                                    if (f.Condition == "on" || f.Condition == "On")
                                    {
                                        var gte = System.Linq.Expressions.Expression.GreaterThanOrEqual(propExpr, constStart);
                                        var lte = System.Linq.Expressions.Expression.LessThanOrEqual(propExpr, constEnd);
                                        conditionExpr = System.Linq.Expressions.Expression.AndAlso(gte, lte);
                                    }
                                    else if (f.Condition == "before") conditionExpr = System.Linq.Expressions.Expression.LessThan(propExpr, constStart);
                                    else if (f.Condition == "after") conditionExpr = System.Linq.Expressions.Expression.GreaterThan(propExpr, constEnd);
                                    else if (f.Condition == "between")
                                    {
                                        var gte = System.Linq.Expressions.Expression.GreaterThanOrEqual(propExpr, constStart);
                                        var lte = System.Linq.Expressions.Expression.LessThanOrEqual(propExpr, constEnd2);
                                        conditionExpr = System.Linq.Expressions.Expression.AndAlso(gte, lte);
                                    }
                                }
                            }
                            // OWNER / USER MAPPING
                            else if (dbColName.EndsWith("OwnerId", StringComparison.OrdinalIgnoreCase) || dbColName == "CreatedBy" || dbColName == "ModifiedBy")
                            {
                                var searchValue = safeValue.ToLower();
                                var matchingUserIds = _context.Users
                                    .Where(u => (u.fullName != null && u.fullName.ToLower().Contains(searchValue)) ||
                                                (u.FirstName != null && u.FirstName.ToLower().Contains(searchValue)))
                                    .Select(u => u.Id).ToList();

                                var listExpr = System.Linq.Expressions.Expression.Constant(matchingUserIds);
                                var containsMethod = typeof(List<string>).GetMethod("Contains", new[] { typeof(string) });

                                if (f.Condition == "contains" || f.Condition == "is")
                                    conditionExpr = System.Linq.Expressions.Expression.Call(listExpr, containsMethod, propExpr);
                                else if (f.Condition == "does_not_contain" || f.Condition == "is_not")
                                    conditionExpr = System.Linq.Expressions.Expression.Not(System.Linq.Expressions.Expression.Call(listExpr, containsMethod, propExpr));
                            }
                            // REGULAR STRINGS
                            else if (propertyInfo.PropertyType == typeof(string))
                            {
                                var valConst = System.Linq.Expressions.Expression.Constant(safeValue);
                                var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes);
                                var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) });
                                var startsWithMethod = typeof(string).GetMethod("StartsWith", new[] { typeof(string) });
                                var endsWithMethod = typeof(string).GetMethod("EndsWith", new[] { typeof(string) });

                                var notNullProp = System.Linq.Expressions.Expression.Coalesce(propExpr, System.Linq.Expressions.Expression.Constant(""));
                                var lowerProp = System.Linq.Expressions.Expression.Call(notNullProp, toLowerMethod);
                                var lowerVal = System.Linq.Expressions.Expression.Constant(safeValue.ToLower());

                                if (f.Condition == "is") conditionExpr = System.Linq.Expressions.Expression.Equal(lowerProp, lowerVal);
                                else if (f.Condition == "is_not") conditionExpr = System.Linq.Expressions.Expression.NotEqual(lowerProp, lowerVal);
                                else if (f.Condition == "contains") conditionExpr = System.Linq.Expressions.Expression.Call(lowerProp, containsMethod, lowerVal);
                                else if (f.Condition == "does_not_contain") conditionExpr = System.Linq.Expressions.Expression.Not(System.Linq.Expressions.Expression.Call(lowerProp, containsMethod, lowerVal));
                                else if (f.Condition == "starts_with") conditionExpr = System.Linq.Expressions.Expression.Call(lowerProp, startsWithMethod, lowerVal);
                                else if (f.Condition == "ends_with") conditionExpr = System.Linq.Expressions.Expression.Call(lowerProp, endsWithMethod, lowerVal);
                                else if (f.Condition == "is_empty") conditionExpr = System.Linq.Expressions.Expression.Call(typeof(string).GetMethod("IsNullOrEmpty"), propExpr);
                                else if (f.Condition == "is_not_empty") conditionExpr = System.Linq.Expressions.Expression.Not(System.Linq.Expressions.Expression.Call(typeof(string).GetMethod("IsNullOrEmpty"), propExpr));
                            }
                            // NUMBERS
                            else
                            {
                                if (propertyInfo.PropertyType == typeof(int) || propertyInfo.PropertyType == typeof(int?))
                                {
                                    if (int.TryParse(safeValue, out int numVal))
                                    {
                                        var valExpr = System.Linq.Expressions.Expression.Constant(numVal, propertyInfo.PropertyType);
                                        if (f.Condition == "is" || f.Condition == "contains") conditionExpr = System.Linq.Expressions.Expression.Equal(propExpr, valExpr);
                                        else if (f.Condition == "is_not" || f.Condition == "does_not_contain") conditionExpr = System.Linq.Expressions.Expression.NotEqual(propExpr, valExpr);
                                    }
                                }
                                else if (propertyInfo.PropertyType == typeof(decimal) || propertyInfo.PropertyType == typeof(decimal?))
                                {
                                    if (decimal.TryParse(safeValue, out decimal decVal))
                                    {
                                        var valExpr = System.Linq.Expressions.Expression.Constant(decVal, propertyInfo.PropertyType);
                                        if (f.Condition == "is" || f.Condition == "contains") conditionExpr = System.Linq.Expressions.Expression.Equal(propExpr, valExpr);
                                        else if (f.Condition == "is_not" || f.Condition == "does_not_contain") conditionExpr = System.Linq.Expressions.Expression.NotEqual(propExpr, valExpr);
                                    }
                                }
                            }

                            if (conditionExpr != null)
                            {
                                if (combinedPredicate == null)
                                {
                                    combinedPredicate = conditionExpr;
                                }
                                else
                                {
                                    if (string.Equals(f.LogicalOperator, "OR", StringComparison.OrdinalIgnoreCase))
                                    {
                                        combinedPredicate = System.Linq.Expressions.Expression.OrElse(combinedPredicate, conditionExpr);
                                    }
                                    else
                                    {
                                        combinedPredicate = System.Linq.Expressions.Expression.AndAlso(combinedPredicate, conditionExpr);
                                    }
                                }
                            }
                        }

                        if (combinedPredicate != null)
                        {
                            var lambda = System.Linq.Expressions.Expression.Lambda<Func<Account, bool>>(combinedPredicate, parameter);
                            query = query.Where(lambda);
                        }

                        // =======================================================
                        // 3. 🚀 PROCESS ZOHO-STYLE RELATED TASKS FILTERS 
                        // =======================================================
                        if (taskFilters.Any())
                        {
                            var presenceFilter = taskFilters.FirstOrDefault(f => f.ColumnName == "RelatedTasksPresence");
                            bool isWithout = presenceFilter != null && presenceFilter.Value.Equals("without", StringComparison.OrdinalIgnoreCase);

                            var taskDetailFilters = taskFilters.Where(f => f.ColumnName.StartsWith("Task.")).ToList();

                            if (isWithout)
                            {
                                query = query.Where(a => !a.Tasks.Any());
                            }
                            else
                            {
                                if (!taskDetailFilters.Any())
                                {
                                    query = query.Where(a => a.Tasks.Any());
                                }
                                else
                                {
                                    var aParam = System.Linq.Expressions.Expression.Parameter(typeof(Account), "a");
                                    var tasksProp = System.Linq.Expressions.Expression.Property(aParam, "Tasks");

                                    var tParam = System.Linq.Expressions.Expression.Parameter(typeof(CrmTaskModel), "t");
                                    System.Linq.Expressions.Expression tCombined = null;

                                    foreach (var f in taskDetailFilters)
                                    {
                                        string tCol = f.ColumnName.Replace("Task.", "");
                                        string tVal = f.Value?.Trim() ?? "";
                                        string lowerVal = tVal.ToLower();
                                        string cond = f.Condition?.ToLower().Trim() ?? "";

                                        if (string.IsNullOrWhiteSpace(tVal) && cond != "is_empty" && cond != "is_not_empty") continue;

                                        System.Linq.Expressions.Expression tCondExpr = null;
                                        var tPropExpr = System.Linq.Expressions.Expression.Property(tParam, tCol);

                                        if (tCol == "Status" || tCol == "Priority")
                                        {
                                            var valConst = System.Linq.Expressions.Expression.Constant(tVal);
                                            if (cond == "is_not") tCondExpr = System.Linq.Expressions.Expression.NotEqual(tPropExpr, valConst);
                                            else tCondExpr = System.Linq.Expressions.Expression.Equal(tPropExpr, valConst);
                                        }
                                        else if (tCol == "Subject")
                                        {
                                            var toLower = typeof(string).GetMethod("ToLower", Type.EmptyTypes);
                                            var contains = typeof(string).GetMethod("Contains", new[] { typeof(string) });
                                            var notNullProp = System.Linq.Expressions.Expression.Coalesce(tPropExpr, System.Linq.Expressions.Expression.Constant(""));
                                            var lowerProp = System.Linq.Expressions.Expression.Call(notNullProp, toLower);
                                            var valConst = System.Linq.Expressions.Expression.Constant(lowerVal);

                                            if (cond == "is_not" || cond == "does_not_contain")
                                                tCondExpr = System.Linq.Expressions.Expression.Not(System.Linq.Expressions.Expression.Call(lowerProp, contains, valConst));
                                            else
                                                tCondExpr = System.Linq.Expressions.Expression.Call(lowerProp, contains, valConst);
                                        }
                                        else if (tCol == "DueDate")
                                        {
                                            DateTime d1 = DateTime.Today, d2 = DateTime.Today; // Safe defaults
                                            bool validDate = false;

                                            if (tVal.Contains("|"))
                                            {
                                                var dates = tVal.Split('|');
                                                if (DateTime.TryParse(dates[0], out DateTime parsedD1) && DateTime.TryParse(dates[1], out DateTime parsedD2))
                                                {
                                                    d1 = parsedD1;
                                                    d2 = parsedD2;
                                                    validDate = true;
                                                }
                                            }
                                            else
                                            {
                                                if (DateTime.TryParse(tVal, out DateTime parsedD1))
                                                {
                                                    d1 = parsedD1;
                                                    validDate = true;
                                                }
                                            }

                                            if (!validDate) continue;

                                            var efMethod = typeof(EF).GetMethod("Property").MakeGenericMethod(typeof(DateTime?));
                                            var efCall = System.Linq.Expressions.Expression.Call(null, efMethod, tParam, System.Linq.Expressions.Expression.Constant(tCol));

                                            var d1Const = System.Linq.Expressions.Expression.Constant((DateTime?)d1.Date, typeof(DateTime?));
                                            var d1EndConst = System.Linq.Expressions.Expression.Constant((DateTime?)d1.Date.AddDays(1).AddTicks(-1), typeof(DateTime?));
                                            var d2EndConst = System.Linq.Expressions.Expression.Constant((DateTime?)d2.Date.AddDays(1).AddTicks(-1), typeof(DateTime?));
                                            var d1NextConst = System.Linq.Expressions.Expression.Constant((DateTime?)d1.Date.AddDays(1), typeof(DateTime?));

                                            if (cond == "before") tCondExpr = System.Linq.Expressions.Expression.LessThan(efCall, d1Const);
                                            else if (cond == "after") tCondExpr = System.Linq.Expressions.Expression.GreaterThan(efCall, d1EndConst);
                                            else if (cond == "between") tCondExpr = System.Linq.Expressions.Expression.AndAlso(System.Linq.Expressions.Expression.GreaterThanOrEqual(efCall, d1Const), System.Linq.Expressions.Expression.LessThanOrEqual(efCall, d2EndConst));
                                            else if (cond == "not between") tCondExpr = System.Linq.Expressions.Expression.OrElse(System.Linq.Expressions.Expression.LessThan(efCall, d1Const), System.Linq.Expressions.Expression.GreaterThan(efCall, d2EndConst));
                                            else tCondExpr = System.Linq.Expressions.Expression.AndAlso(System.Linq.Expressions.Expression.GreaterThanOrEqual(efCall, d1Const), System.Linq.Expressions.Expression.LessThan(efCall, d1NextConst));
                                        }

                                        if (tCondExpr != null)
                                        {
                                            tCombined = tCombined == null ? tCondExpr : System.Linq.Expressions.Expression.AndAlso(tCombined, tCondExpr);
                                        }
                                    }

                                    if (tCombined != null)
                                    {
                                        var taskLambda = System.Linq.Expressions.Expression.Lambda<Func<CrmTaskModel, bool>>(tCombined, tParam);
                                        var anyMethod = typeof(Enumerable).GetMethods().First(m => m.Name == "Any" && m.GetParameters().Length == 2).MakeGenericMethod(typeof(CrmTaskModel));
                                        var anyCall = System.Linq.Expressions.Expression.Call(anyMethod, tasksProp, taskLambda);
                                        var accountLambda = System.Linq.Expressions.Expression.Lambda<Func<Account, bool>>(anyCall, aParam);

                                        query = query.Where(accountLambda);
                                    }
                                    else
                                    {
                                        query = query.Where(a => a.Tasks.Any());
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("\n=========================================");
                    Console.WriteLine("FILTER ENGINE CRASH LOG:");
                    Console.WriteLine(ex.Message);
                    Console.WriteLine("=========================================\n");
                }
            }

            if (!string.IsNullOrEmpty(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(a =>
                    (a.AccountName != null && a.AccountName.ToLower().Contains(searchLower)) ||
                    (a.MobileNumber != null && a.MobileNumber.Contains(search)) ||
                    (a.Email != null && a.Email.ToLower().Contains(searchLower))
                );
            }

            if (sortDir == "desc")
            {
                query = sortCol switch
                {
                    "AccountName" => query.OrderByDescending(a => a.AccountName),
                    "MobileNumber" => query.OrderByDescending(a => a.MobileNumber),
                    "CurrentStatus" => query.OrderByDescending(a => a.CurrentStatus),
                    "ModifiedDateAndTime" => query.OrderByDescending(a => a.ModifiedDateAndTime),
                    _ => query.OrderByDescending(a => a.CreatedDateAndTime) // 🚀 Changed fallback to CreatedDateAndTime
                };
            }
            else
            {
                query = sortCol switch
                {
                    "AccountName" => query.OrderBy(a => a.AccountName),
                    "MobileNumber" => query.OrderBy(a => a.MobileNumber),
                    "CurrentStatus" => query.OrderBy(a => a.CurrentStatus),
                    "ModifiedDateAndTime" => query.OrderBy(a => a.ModifiedDateAndTime),
                    _ => query.OrderBy(a => a.CreatedDateAndTime) // 🚀 Changed fallback to CreatedDateAndTime
                };
            }

            var totalRecords = await query.CountAsync();

            if (page < 1) page = 1;

            int totalPagesCalc = (int)Math.Ceiling(totalRecords / (double)pageSize);
            if (totalPagesCalc == 0) totalPagesCalc = 1;

            if (page > totalPagesCalc) page = totalPagesCalc;

            var accounts = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPagesCalc;
            ViewBag.TotalRecords = totalRecords;
            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");

            // 🚀 NEW: Safely get note counts only for the loaded accounts (No Include needed!)
            var accountIds = accounts.Select(a => a.Id).ToList();
            var noteCounts = await _context.Note
                .Where(n => n.AccountId != null && accountIds.Contains(n.AccountId.Value))
                .GroupBy(n => n.AccountId.Value)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            ViewBag.NoteCounts = noteCounts; // Send dictionary to the view

            // 1. Get the IDs of the accounts currently being displayed on the page
            var currentAccountIds = accounts.Select(a => a.Id).ToList();

            // 2. Fetch the most urgent/upcoming task for each of these accounts
            // 🚀 FIX: Added t.AccountId.HasValue and .Value to safely handle the int? conversion
            var upcomingTasks = _context.Tasks
                .Where(t => t.AccountId.HasValue && currentAccountIds.Contains(t.AccountId.Value) && t.Status != "Completed")
                .ToList()
                .GroupBy(t => t.AccountId.Value) // Group by the strict int value
                .Select(g => g.OrderBy(t => t.DueDate).FirstOrDefault())
                .ToList();

            // 3. Map it to the Dictionary format that the Razor View expects
            var accountTasksDict = new Dictionary<int, dynamic>();

            foreach (var task in upcomingTasks)
            {
                if (task != null && task.AccountId.HasValue)
                {
                    // 🚀 FIX: Use .Value to convert int? to int for the Dictionary key
                    accountTasksDict[task.AccountId.Value] = new
                    {
                        Subject = task.Subject,
                        DueDate = task.DueDate
                    };
                }
            }

            // 4. Pass it to the ViewBag
            ViewBag.AccountTasks = accountTasksDict;
            ViewBag.VendorsList = new SelectList(_context.Vendors, "Id", "VendorName");

            return View(accounts);
        }


        // Replace the existing class in AccountsController.cs with this:
        public class FilterCriteria
        {
            // 🚀 ADDING THIS HERE FIXES THE ERROR FOR BOTH LEADS AND ACCOUNTS
            public string LogicalOperator { get; set; }

            public string ColumnName { get; set; }
            public string Condition { get; set; }
            public string Value { get; set; }
            public bool IsDate { get; set; }
        }
        // ==========================================
        // HELPER METHOD (Add this to the bottom of the AccountsController)
        // ==========================================
        private List<int> GetSubordinateRoleIds(List<Role> allRoles, int? currentRoleId)
        {
            var subordinateIds = new List<int>();
            if (currentRoleId == null) return subordinateIds;

            var directChildren = allRoles.Where(r => r.ParentRoleId == currentRoleId).Select(r => r.Id).ToList();
            subordinateIds.AddRange(directChildren);

            foreach (var childId in directChildren)
            {
                subordinateIds.AddRange(GetSubordinateRoleIds(allRoles, childId));
            }

            return subordinateIds;
        }

        // ==========================================
        // DYNAMIC FIELD OPTIONS FOR MASS UPDATE
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> GetFieldOptions(string fieldName)
        {
            try
            {
                var propertyInfo = typeof(Account).GetProperty(fieldName, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                // If the field isn't a string (dropdowns are usually strings), return empty so JS shows a textbox
                if (propertyInfo == null || propertyInfo.PropertyType != typeof(string))
                {
                    return Json(new string[] { });
                }

                // 🚀 Query the DB for all unique, non-null values that currently exist for this column
                var distinctValues = await _context.Accounts
                    .Select(a => EF.Property<string>(a, propertyInfo.Name))
                    .Where(v => !string.IsNullOrEmpty(v))
                    .Distinct()
                    .OrderBy(v => v)
                    .ToListAsync();

                return Json(distinctValues);
            }
            catch
            {
                return Json(new string[] { });
            }
        }

        // ==========================================
        // BULK UPDATE: AJAX POST
        // ==========================================
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> BulkUpdate([FromBody] BulkUpdateRequest request)
        {
            if (request == null || request.Ids == null || !request.Ids.Any() || string.IsNullOrEmpty(request.FieldName))
            {
                return Json(new { success = false, message = "Invalid data provided." });
            }

            try
            {
                var accountsToUpdate = await _context.Accounts.Where(a => request.Ids.Contains(a.Id)).ToListAsync();
                var propertyInfo = typeof(Account).GetProperty(request.FieldName, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (propertyInfo == null)
                    return Json(new { success = false, message = "Field not found in database." });
                var updatedAccountsList = new Dictionary<int, string>();

                foreach (var account in accountsToUpdate)
                {
                    object safeValue = null;
                    Type t = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;

                    if (!string.IsNullOrWhiteSpace(request.NewValue))
                    {
                        if (t == typeof(DateTime))
                        {
                            if (DateTime.TryParse(request.NewValue, out DateTime d)) safeValue = d;
                        }
                        else
                        {
                            safeValue = Convert.ChangeType(request.NewValue, t);
                        }
                    }

                    propertyInfo.SetValue(account, safeValue, null);

                    account.ModifiedDateAndTime = DateTime.Now;

                    // Add ID and Name to dictionary
                    updatedAccountsList[account.Id] = account.AccountName ?? "Unknown Account";
                }

                _context.UpdateRange(accountsToUpdate);
                await _context.SaveChangesAsync();

                // 🚀 TRIGGER EMAIL IF OWNER WAS CHANGED
                if (request.FieldName.Equals("AccountOwnerId", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(request.NewValue))
                {
                    await NotifyNewOwnerAsync(request.NewValue, updatedAccountsList);
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                string trueError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = trueError });
            }
        }
        // 🚀 Ensure this class is defined inside the controller for the JSON binding to work
        public class BulkUpdateRequest
        {
            public List<int> Ids { get; set; }
            public string FieldName { get; set; }
            public string NewValue { get; set; }
        }

        // ==========================================
        // AJAX: GET NOTES FOR OFFCANVAS
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> GetNotes(int accountId)
        {
            var notes = await _context.Note
                .Where(n => n.AccountId == accountId)
                .OrderByDescending(n => n.CreatedDateTime)
                .Select(n => new
                {
                    ownerName = n.NoteOwner != null ? n.NoteOwner.fullName : "System",
                    createdDateTime = n.CreatedDateTime.ToString("MMM dd, yyyy h:mm tt"),
                    noteTitle = n.NoteTitle, // 🚀 THIS MUST BE HERE
                    description = n.Description,
                    attachmentFileName = n.AttachmentFileName
                }).ToListAsync();

            return Json(notes);
        }
        [HttpPost]
        public async Task<IActionResult> SaveNoteAjax(int accountId, string noteTitle, string description, string ownerId, IFormFile attachment)
        {
            try
            {
                // 1. Fallback to current logged-in user's ID if the UI dropdown was left blank
                string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                string finalOwnerId = string.IsNullOrWhiteSpace(ownerId) ? currentUserId : ownerId;

                // 2. Safely resolve the correct Name for the UI
                string ownerName = User.Identity?.Name ?? "System User"; // Default to identity name

                if (!string.IsNullOrWhiteSpace(finalOwnerId))
                {
                    var user = await _context.Users.FindAsync(finalOwnerId);
                    if (user != null)
                    {
                        ownerName = user.fullName ?? user.FirstName ?? ownerName;
                    }
                }

                var note = new Notes
                {
                    AccountId = accountId,
                    NoteOwnerId = finalOwnerId,
                    NoteTitle = noteTitle ?? "",
                    NoteContent = description ?? "",
                    Description = description ?? "",
                    CreatedDateTime = DateTime.Now
                };

                if (attachment != null && attachment.Length > 0)
                {
                    string uploadPath = @"C:\CRM_Files\Notes";
                    if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

                    string fileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(attachment.FileName);
                    string filePath = Path.Combine(uploadPath, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await attachment.CopyToAsync(stream);
                    }

                    note.AttachmentFileName = attachment.FileName;
                    note.AttachmentFilePath = filePath;
                }

                _context.Note.Add(note);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    note = new
                    {
                        ownerName = ownerName,
                        createdDateTime = note.CreatedDateTime.ToString("MMM dd, yyyy h:mm tt"),
                        noteTitle = note.NoteTitle,
                        description = note.Description,
                        attachmentFileName = note.AttachmentFileName
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ==========================================
        // EDIT NOTE VIA AJAX
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> EditNoteAjax(int noteId, string noteTitle, string description)
        {
            try
            {
                var note = await _context.Note.FindAsync(noteId);
                if (note == null) return Json(new { success = false, message = "Note not found in the database." });

                // Update the note's text fields
                note.NoteTitle = noteTitle ?? "";
                note.Description = description ?? "";
                note.NoteContent = description ?? ""; // Keeping in sync with your create logic

                _context.Note.Update(note);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ==========================================
        // CREATE: GET (Opens the blank form)
        // ==========================================
        public IActionResult Create()
        {
            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            ViewBag.VendorsList = new SelectList(_context.Vendors, "Id", "VendorName");

            // 1. Get the currently logged-in user's ID
            string loggedInUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // 2. Create a new model and pre-fill the AccountOwnerId
            var model = new Account
            {
                AccountOwnerId = loggedInUserId
            };

            // 3. Pass the model to the view
            return View(model);
        }

        // ==========================================
        // CREATE: POST (Saves new data to the DB)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Account account, string[] SavedNoteOwner, string[] SavedNoteDateTime, string[] SavedNoteDesc, List<IFormFile> SavedNoteFiles)
        {
            account.DateOfEntry = DateTime.Now;
            account.CreatedDateAndTime = DateTime.Now;
            account.ModifiedDateAndTime = DateTime.Now;

            ModelState.Remove("DateOfEntry");
            ModelState.Remove("CreatedDateAndTime"); // NEW
            ModelState.Remove("ModifiedDateAndTime"); // NEW

            // 🚀 SET "CREATED BY" WITH CURRENT LOGGED-IN USER & TIME
            string currentUser = User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(User.Identity.Name) ? User.Identity.Name : "System User";
            account.CreatedBy = $"{currentUser} on {DateTime.Now.ToString("MMM dd, yyyy - hh:mm tt")}";
            ModelState.Remove("CreatedBy");

            if (ModelState.IsValid)
            {
                _context.Add(account);
                await _context.SaveChangesAsync();

                await SaveNotesAsync(account.Id, "Account", SavedNoteOwner, SavedNoteDateTime, SavedNoteDesc, SavedNoteFiles);
                await CheckAndCreateContactFromAccountAsync(account);

                // 🚀 FIX: This ensures the JavaScript automatically redirects to the Edit Page!
                return RedirectToAction("Edit", new { id = account.Id });
            }

            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            ViewBag.VendorsList = new SelectList(_context.Vendors, "Id", "VendorName");
            return View(account);
        }
        // ==========================================
        // EDIT: GET (Fetches specific record & automatically linked data)
        // ==========================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var account = await _context.Accounts.FindAsync(id);
            if (account == null) return NotFound();

            // 1. Fetch Existing Notes
            ViewBag.ExistingNotes = await _context.Note
                .Include(n => n.NoteOwner)
                .Where(n => n.AccountId == id)
                .OrderByDescending(n => n.CreatedDateTime)
                .ToListAsync();

            // 2. Fetch Automatically Linked Leads (Contacts)
            var relatedLeads = await _context.Leads
                .Where(l => l.AccountId == id)
                .ToListAsync();

            ViewBag.RelatedLeads = relatedLeads;

            // 🚀 NEW: Safely pull the LeadStatus from the linked Lead into the Account
            var linkedLeadWithStatus = relatedLeads.FirstOrDefault(l => !string.IsNullOrEmpty(l.LeadStatus) || !string.IsNullOrEmpty(l.Stage));
            if (linkedLeadWithStatus != null)
            {
                if (!string.IsNullOrEmpty(linkedLeadWithStatus.LeadStatus))
                {
                    account.LeadStatus = linkedLeadWithStatus.LeadStatus;
                }

                // 🚀 ADDED: Pull Stage from Lead into Account's LeadStage property
                if (!string.IsNullOrEmpty(linkedLeadWithStatus.Stage))
                {
                    account.LeadStage = linkedLeadWithStatus.Stage;
                }
            }

            // 3. Fetch Automatically Linked Deals
            ViewBag.RelatedDeals = await _context.Set<Deal>()
                .Where(d => d.AccountId == id)
                .ToListAsync();

            // 4. Fetch Automatically Linked Tasks
            ViewBag.RelatedTasks = await _context.Set<Radar_CRM.Models.Task>()
                .Where(t => t.AccountId == id)
                .ToListAsync();

            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            ViewBag.VendorsList = new SelectList(_context.Vendors, "Id", "VendorName");

            return View(account);
        }
        // ==========================================
        // EDIT: POST (Saves changes back to the DB)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Account account, string[] SavedNoteOwner, string[] SavedNoteDateTime, string[] SavedNoteDesc, List<IFormFile> SavedNoteFiles)
        {
            if (id != account.Id) return NotFound();

            // 🚀 FIX 1: PROFILE COMPLETION & STATE CHECK
            bool isProfileIncomplete =
                string.IsNullOrWhiteSpace(account.IsHomeopathicDoctor) || account.IsHomeopathicDoctor == "-None-" || account.IsHomeopathicDoctor == "false" ||
                string.IsNullOrWhiteSpace(account.WorkType) || account.WorkType == "-None-" || account.WorkType == "false" ||
                string.IsNullOrWhiteSpace(account.ClinicType) || account.ClinicType == "-None-" || account.ClinicType == "false" ||
                string.IsNullOrWhiteSpace(account.HasComputer) || account.HasComputer == "-None-" || account.HasComputer == "false";

            bool hasState = !string.IsNullOrWhiteSpace(account.Addr1_State) && account.Addr1_State != "-None-" && account.Addr1_State != "- Select State -";

            // If profile is incomplete AND they haven't provided a State, they MUST provide a Pending Reason
            if (isProfileIncomplete && !hasState && (string.IsNullOrWhiteSpace(account.ProfilePendingReason) || account.ProfilePendingReason == "-None-"))
            {
                ModelState.AddModelError("ProfilePendingReason", "Your profile is incomplete. You must provide a Profile Pending Reason or a State.");
                TempData["ErrorMessage"] = "Your profile is incomplete. You must provide a Profile Pending Reason or a State.";
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Get Current User Info for Logging
                    string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "System";
                    string currentUser = User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(User.Identity.Name) ? User.Identity.Name : "System User";

                    // 🚀 SET "MODIFIED BY" WITH CURRENT LOGGED-IN USER & TIME
                    account.ModifiedBy = $"{currentUser} on {DateTime.Now.ToString("MMM dd, yyyy - hh:mm tt")}";
                    account.ModifiedDateAndTime = DateTime.Now; // NEW

                    ModelState.Remove("ModifiedBy");
                    ModelState.Remove("ModifiedDateAndTime"); // NEW

                    // Fetch the original record from the database to check the old owner & track timeline changes
                    var existingAccount = await _context.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == account.Id);


                    // 🚀 NEW: Preserve CreatedDateAndTime so it isn't accidentally erased during edit
                    if (existingAccount != null && account.CreatedDateAndTime == null)
                    {
                        account.CreatedDateAndTime = existingAccount.CreatedDateAndTime;
                    }

                    // Ensure CreatedBy is not accidentally erased during an edit
                    if (existingAccount != null && string.IsNullOrEmpty(account.CreatedBy))
                    {
                        account.CreatedBy = existingAccount.CreatedBy;
                    }

                    // =========================================================
                    // 🚀 FIX 2: ZOHO-STYLE TIMELINE TRACKER ENGINE
                    // =========================================================
                    if (existingAccount != null)
                    {
                        var timelineEvents = new List<TimelineEvent>();
                        var excludedAuditFields = new[] { "ModifiedBy", "ModifiedTime", "CreatedBy", "DateOfEntry" };

                        // Dynamically compare every single property on the Account
                        var properties = typeof(Account).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                            .Where(p => !excludedAuditFields.Contains(p.Name) && (!p.PropertyType.IsGenericType || p.PropertyType.GetGenericTypeDefinition() != typeof(List<>)));

                        foreach (var prop in properties)
                        {
                            var oldObj = prop.GetValue(existingAccount);
                            var newObj = prop.GetValue(account);

                            string oldVal = oldObj?.ToString()?.Trim() ?? "blank value";
                            string newVal = newObj?.ToString()?.Trim() ?? "blank value";

                            // If the field changed, log it!
                            if (oldVal != newVal && !(oldVal == "blank value" && newVal == ""))
                            {
                                timelineEvents.Add(new TimelineEvent
                                {
                                    EntityType = "Account",
                                    EntityId = account.Id,
                                    EventType = "FieldUpdate",
                                    FieldName = prop.Name,
                                    OldValue = oldVal,
                                    NewValue = newVal,
                                    ActionDescription = $"{prop.Name} was updated from {oldVal} to {newVal}",
                                    CreatedById = currentUserId,
                                    CreatedByName = currentUser,
                                    CreatedDate = DateTime.Now
                                });
                            }
                        }

                        if (timelineEvents.Any())
                        {
                            await _context.TimelineEvents.AddRangeAsync(timelineEvents);
                        }
                    }
                    // =========================================================

                    // Ensure CreatedBy is not accidentally erased during an edit
                    if (existingAccount != null && string.IsNullOrEmpty(account.CreatedBy))
                    {
                        account.CreatedBy = existingAccount.CreatedBy;
                    }

                    // Compare the database owner to the new form owner
                    bool ownerChanged = existingAccount != null && existingAccount.AccountOwnerId != account.AccountOwnerId;

                    // Save the new changes
                    _context.Update(account);
                    await _context.SaveChangesAsync();

                    await SaveNotesAsync(account.Id, "Account", SavedNoteOwner, SavedNoteDateTime, SavedNoteDesc, SavedNoteFiles);
                    await CheckAndCreateContactFromAccountAsync(account);

                    // 🚀 TRIGGER EMAIL IF OWNER WAS CHANGED
                    if (ownerChanged && !string.IsNullOrWhiteSpace(account.AccountOwnerId))
                    {
                        var accountDict = new Dictionary<int, string> { { account.Id, account.AccountName ?? "Unknown Account" } };
                        await NotifyNewOwnerAsync(account.AccountOwnerId, accountDict);
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AccountExists(account.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            // Fallback if ModelState is invalid
            ViewBag.ExistingNotes = await _context.Note
                .Where(n => n.AccountId == id)
                .OrderByDescending(n => n.CreatedDateTime)
                .ToListAsync();

            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            ViewBag.VendorsList = new SelectList(_context.Vendors, "Id", "VendorName");

            return View(account);
        }

        // ==========================================
        // HELPER: Save Notes & Files to C Drive
        // ==========================================
        private async Task SaveNotesAsync(int recordId, string moduleName, string[] owners, string[] dateTimes, string[] descs, List<IFormFile> files)
        {
            string uploadPath = @"C:\CRM_Files\Notes";

            if (!Directory.Exists(uploadPath))
            {
                Directory.CreateDirectory(uploadPath);
            }

            // 1. Fetch all valid User IDs from the database to be 100% certain
            var validUserIds = await _context.Users.Select(u => u.Id).ToListAsync();

            if (descs != null && descs.Length > 0)
            {
                for (int i = 0; i < descs.Length; i++)
                {
                    // Only save if there is text OR a file attached
                    if (!string.IsNullOrWhiteSpace(descs[i]) || (files != null && i < files.Count && files[i] != null && files[i].Length > 0))
                    {
                        // 2. BULLETPROOF FOREIGN KEY FIX:
                        // Extract whatever the frontend sent (could be an ID, could be "System User", could be empty)
                        string incomingOwner = (owners != null && owners.Length > i) ? owners[i]?.Trim() : null;

                        // 3. Only assign the ID if it STRICTLY exists in the database. Otherwise, force it to NULL.
                        string finalOwnerId = null;
                        if (!string.IsNullOrEmpty(incomingOwner) && validUserIds.Contains(incomingOwner))
                        {
                            finalOwnerId = incomingOwner;
                        }

                        var newNote = new Notes
                        {
                            AccountId = recordId,
                            NoteOwnerId = finalOwnerId, // Guaranteed to either be a real User ID or safely NULL
                            CreatedDateTime = dateTimes != null && dateTimes.Length > i && DateTime.TryParse(dateTimes[i], out DateTime parsedDate) ? parsedDate : DateTime.Now,
                            Description = descs[i]
                        };

                        // Handle File Attachment
                        if (files != null && i < files.Count && files[i] != null && files[i].Length > 0)
                        {
                            var file = files[i];

                            string fileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
                            string filePath = Path.Combine(uploadPath, fileName);

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(stream);
                            }

                            newNote.AttachmentFileName = file.FileName;
                            newNote.AttachmentFilePath = filePath;
                        }

                        _context.Note.Add(newNote);
                    }
                }
                await _context.SaveChangesAsync();
            }
        }

        [HttpGet]
        public IActionResult DownloadNoteAttachment(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return BadRequest("Filename missing.");

            string folderPath = @"C:\CRM_Files\Notes";

            // 1. Try exact match first
            string filePath = Path.Combine(folderPath, fileName);

            // 2. If exact match fails, search for the file with the GUID prefix
            if (!System.IO.File.Exists(filePath))
            {
                // Searches for any file ending with "_Screenshot (5).png"
                var matchingFiles = Directory.GetFiles(folderPath, "*_" + fileName);
                if (matchingFiles.Length > 0)
                {
                    filePath = matchingFiles[0]; // Grab the actual file with the GUID
                }
                else
                {
                    return NotFound("File not found on server.");
                }
            }

            byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);
            return File(fileBytes, "application/octet-stream", fileName);
        }

        // ==========================================
        // ACTION: Download/View Saved File
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> DownloadNoteFile(int noteId)
        {
            var note = await _context.Note.FindAsync(noteId);
            if (note == null || string.IsNullOrEmpty(note.AttachmentFilePath) || !System.IO.File.Exists(note.AttachmentFilePath))
            {
                return NotFound("File not found on server.");
            }

            var memory = new MemoryStream();
            using (var stream = new FileStream(note.AttachmentFilePath, FileMode.Open, FileAccess.Read))
            {
                await stream.CopyToAsync(memory);
            }
            memory.Position = 0;

            return File(memory, "application/octet-stream", note.AttachmentFileName);
        }

        // ==========================================
        // AUTOMATION: Condition Check & Record Creation
        // ==========================================
        private async Task CheckAndCreateContactFromAccountAsync(Account acc)
        {
            // 1. Specific Condition: Must be explicitly marked as 'Qualified'
            if (acc.QualificationStatus != "Qualified") return;

            // 2. Count the specific required fields
            int filledFieldsCount = 0;

            if (!string.IsNullOrWhiteSpace(acc.IsHomeopathicDoctor)) filledFieldsCount++;
            if (!string.IsNullOrWhiteSpace(acc.WorkType)) filledFieldsCount++;
            if (!string.IsNullOrWhiteSpace(acc.HasComputer)) filledFieldsCount++;
            if (!string.IsNullOrWhiteSpace(acc.ClinicType)) filledFieldsCount++;
            if (!string.IsNullOrWhiteSpace(acc.Qualification)) filledFieldsCount++;
            if (!string.IsNullOrWhiteSpace(acc.CollegeName)) filledFieldsCount++;
            if (!string.IsNullOrWhiteSpace(acc.YearOfPassing)) filledFieldsCount++;
            if (acc.TotalExperience.HasValue) filledFieldsCount++;
            if (acc.YearsOfPractice.HasValue) filledFieldsCount++;
            if (acc.AveragePatientFee.HasValue) filledFieldsCount++;
            if (acc.NumberOfClinics.HasValue) filledFieldsCount++;
            if (acc.DateOfBirth.HasValue) filledFieldsCount++;
            if (acc.Age.HasValue) filledFieldsCount++;
            if (acc.PatientsPerDay.HasValue) filledFieldsCount++;

            bool hasComputer = acc.HasComputer != "No";
            bool isHomeopath = acc.IsHomeopathicDoctor != "No" && acc.IsHomeopathicDoctor != "नहीं";

            // 3. Determine Profile Status based on logic
            string profileStatus = "Not Completed";

            if (filledFieldsCount > 4 && hasComputer && isHomeopath)
            {
                profileStatus = "Strongly Completed";
            }
            else if (filledFieldsCount == 4 && hasComputer && isHomeopath)
            {
                profileStatus = "Completed";
            }
            else if (filledFieldsCount >= 4 && (!hasComputer || !isHomeopath))
            {
                profileStatus = "Not-Qualified";
            }

            // 4. Update the Account record silently
            acc.Profilestatus = profileStatus;
            acc.ProfileRate = filledFieldsCount;

            _context.Accounts.Update(acc);
            await _context.SaveChangesAsync();

            // 5. Condition Check: Automatically trigger Contact creation if conditions are met
            if (profileStatus == "Strongly Completed" || profileStatus == "Completed")
            {
                // Duplicate Check: Ensure the record doesn't already exist in Contacts
                bool recordExists = await _context.Leads.AnyAsync(c => c.AccountId == acc.Id || c.MobileNumber == acc.MobileNumber);
                if (recordExists) return;

                // ==========================================
                // OWNER ASSIGNMENT LOGIC 
                // ==========================================
                string assignedOwnerId = acc.AccountOwnerId;

                // 🚀 FIX: Ensure we retrieve the user safely to grab their full name string
                var ownerUser = await _context.Users.FindAsync(acc.AccountOwnerId);
                string ownerFullName = ownerUser?.fullName ?? ownerUser?.FirstName ?? "System User";

                // 🚀 FIX: Removed Vishakha from here. Now ONLY Mamta assigns to Aman. 
                if (ownerFullName.Contains("Mamta", StringComparison.OrdinalIgnoreCase))
                {
                    var amanUser = await _context.Users.FirstOrDefaultAsync(u => u.FirstName.Contains("Aman"));
                    if (amanUser != null)
                    {
                        assignedOwnerId = amanUser.Id;
                        ownerFullName = amanUser.fullName ?? amanUser.FirstName; // Capture Aman's name
                    }
                }

                // 🚀 NEW FIX: Grab the live user who is actively clicking "Save" triggering this automation
                string currentUser = User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(User.Identity.Name)
                                     ? User.Identity.Name
                                     : "System Automation";
                string createdByString = $"{currentUser} on {DateTime.Now.ToString("MMM dd, yyyy - hh:mm tt")}";

                // Creating as a Contact Record within the Lead model structure
                var newContact = new Lead
                {
                    // --- Core Identifiers ---
                    ContactName = acc.ContactPersonName ?? "Unknown Contact",
                    LeadName = acc.AccountName ?? "Unknown Lead", // Required in UI
                    AccountId = acc.Id,

                    Stage = "Open",

                    // 🚀 FIX: Apply the live captured user string instead of acc.CreatedBy
                    CreatedBy = createdByString,


                    // --- Ownership Mapping ---
                    LeadOwnerId = assignedOwnerId,
                    AccountOwnerId = acc.AccountOwnerId,
                    CoOwnerId = acc.CoOwnerId,

                    // --- Source Mapping ---
                    DataSources = acc.DataSource,
                    CampaignSource = acc.DataSource,
                    GroupName = acc.GroupName, // Mapped here as well in case the UI expects it

                    // --- Basic Details ---
                    MobileNumber = acc.MobileNumber,
                    AlternateMobile = acc.AlternateMobile,
                    EmailID = acc.Email,
                    AlternateEmailID = acc.AlternateEmailID,
                    AccountType = acc.AccountType,
                    CurrentStatus = acc.CurrentStatus,
                    MetaCampaignName = acc.MetaCampaignName,
                    SeminarName = acc.SeminarName,
                    // --- Address Mapping ---
                    Addr1_Country = acc.Addr1_Country,
                    Addr1_FlatHouse = acc.Addr1_FlatHouse,
                    Addr1_Street = acc.Addr1_Street,
                    Addr1_City = acc.Addr1_City,
                    Addr1_State = acc.Addr1_State,
                    Addr1_Zip = acc.Addr1_Zip,

                    Addr2_Country = acc.Addr2_Country,
                    Addr2_FlatHouse = acc.Addr2_FlatHouse,
                    Addr2_Street = acc.Addr2_Street,
                    Addr2_City = acc.Addr2_City,
                    Addr2_State = acc.Addr2_State,
                    Addr2_Zip = acc.Addr2_Zip,

                    // --- Professional Mapping ---
                    IsHomeopathicDoctor = acc.IsHomeopathicDoctor,
                    ClinicType = acc.ClinicType,
                    WorkType = acc.WorkType,
                    HasComputer = acc.HasComputer,
                    DateOfBirth = acc.DateOfBirth,
                    Age = acc.Age,
                    YearOfPractice = acc.YearsOfPractice,
                    AveragePatientFee = acc.AveragePatientFee,
                    PatientsPerDay = acc.PatientsPerDay,
                    TotalExperience = acc.TotalExperience,
                    NumberOfClinics = acc.NumberOfClinics,
                    Qualification = acc.Qualification,
                    YearOfPassing = acc.YearOfPassing,
                    CollegeName = acc.CollegeName,

                    // --- Software & Purchases ---
                    CurrentlyUsingSoftware = acc.CurrentlyUsingSoftware,
                    CurrentSoftwareName = acc.CurrentSoftwareName,
                    PurchaseDate = acc.PurchaseDate,
                    PurchaseValue = acc.PurchaseValue,
                    PaymentType = acc.PaymentType,
                    PaymentStatus = acc.PaymentStatus,

                    ContactPerson1 = acc.ContactPerson1,
                    Contact1Phone = acc.Contact1Phone,
                    ContactPerson2 = acc.ContactPerson2,
                    Contact2Phone = acc.Contact2Phone,
                    ContactPerson3 = acc.ContactPerson3,
                    Contact3Phone = acc.Contact3Phone,

                    Description = acc.Description,
                    // --- System Logic Default Fields ---
                  
                    CreatedDateAndTime = DateTime.Now,
                    LeadCreatedTime = DateTime.Now,
                    Pipeline = acc.CurrentStatus == "User" ? "Upgrade Software" : "New Software"
                };


                // 1. SAVE THE NEW CONTACT FIRST
                _context.Leads.Add(newContact);
                await _context.SaveChangesAsync(); // This generates the newContact.Id

                // ==========================================
                // 2. 🚀 NEW: COPY/TRANSFER NOTES TO THE NEW CONTACT
                // ==========================================

                //var accountNotes = await _context.Note
                //     .Where(n => n.AccountId == acc.Id)
                //     .AsNoTracking() // Ensures we create new copies
                //     .ToListAsync();

                //if (accountNotes.Any())
                //{
                //    // Fetch valid user IDs to prevent Foreign Key crashes if NoteOwnerId is invalid
                //    var validUserIds = await _context.Users.Select(u => u.Id).ToListAsync();

                //    // Declaring contactNotes so Entity Framework can save it
                //    var contactNotes = new List<Notes>();

                //    foreach (var note in accountNotes)
                //    {
                //        contactNotes.Add(new Notes
                //        {
                //            LeadId = newContact.Id,
                //            AccountId = null, // Set to null so it uniquely maps to the new Lead/Contact record

                //            // EXACT COPY: Safely transfer Owner, Date, Time, and Text
                //            NoteOwnerId = (!string.IsNullOrEmpty(note.NoteOwnerId) && validUserIds.Contains(note.NoteOwnerId)) ? note.NoteOwnerId : null,
                //            CreatedDateTime = note.CreatedDateTime,
                //            Description = note.Description,

                //            AttachmentFileName = note.AttachmentFileName,
                //            AttachmentFilePath = note.AttachmentFilePath
                //        });
                //    }
                //    await _context.Note.AddRangeAsync(contactNotes);
                //    await _context.SaveChangesAsync();
                //}


                // ==========================================
                // 3. 🚀 TRANSFER INCOMPLETE TASKS TO THE NEW LEAD
                // ==========================================
                // Fetch tasks linked to the Account that are NOT Completed
                var incompleteTasks = await _context.Set<Radar_CRM.Models.Task>()
                     .Where(t => t.AccountId == acc.Id && t.Status != "Completed")
                     .ToListAsync();

                if (incompleteTasks.Any())
                {
                    foreach (var task in incompleteTasks)
                    {
                        // Disconnect from the Account and attach to the newly created Lead
                        task.AccountId = null;
                        task.LeadId = newContact.Id;

                        // Optionally log that the system moved it
                        task.ModifiedBy = $"{currentUser} (System Transfer) on {DateTime.Now.ToString("MMM dd, yyyy - hh:mm tt")}";
                        task.ModifiedTime = DateTime.Now;
                    }

                    _context.Set<Radar_CRM.Models.Task>().UpdateRange(incompleteTasks);
                    await _context.SaveChangesAsync();
                }
            }
        }


        // ==========================================
        // DELETE: GET (Fetches record for confirmation)
        // ==========================================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var account = await _context.Accounts.FirstOrDefaultAsync(m => m.Id == id);
            if (account == null) return NotFound();

            return View(account);
        }

        // ==========================================
        // DELETE: POST (Actually removes from DB)
        // ==========================================
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var account = await _context.Accounts.FindAsync(id);
            if (account != null)
            {
                // 1. Delete Tasks
                var relatedTasks = await _context.Set<Radar_CRM.Models.Task>().Where(t => t.AccountId == id).ToListAsync();
                if (relatedTasks.Any()) _context.Set<Radar_CRM.Models.Task>().RemoveRange(relatedTasks);

                // 2. Delete Deals
                var relatedDeals = await _context.Set<Deal>().Where(d => d.AccountId == id).ToListAsync();
                if (relatedDeals.Any()) _context.Set<Deal>().RemoveRange(relatedDeals);

                // 3. 🚀 FIX: Find Leads, delete their Notes FIRST, then delete Leads
                if (_context.Leads != null)
                {
                    var relatedLeads = await _context.Leads.Where(l => l.AccountId == id).ToListAsync();
                    if (relatedLeads.Any())
                    {
                        var leadIds = relatedLeads.Select(l => l.Id).ToList();

                        // Delete Notes attached to these Leads to satisfy FK_Note_Leads_LeadId
                        if (_context.Note != null)
                        {
                            var leadNotes = await _context.Note.Where(n => n.LeadId != null && leadIds.Contains((int)n.LeadId)).ToListAsync();
                            if (leadNotes.Any()) _context.Note.RemoveRange(leadNotes);
                        }

                        _context.Leads.RemoveRange(relatedLeads);
                    }
                }

                // 4. Delete Notes attached directly to the Account
                if (_context.Note != null)
                {
                    var relatedNotes = await _context.Note.Where(n => n.AccountId == id).ToListAsync();
                    if (relatedNotes.Any()) _context.Note.RemoveRange(relatedNotes);
                }

                // 5. Now it's safe to remove the account!
                _context.Accounts.Remove(account);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> CheckEmailDuplicate(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return Json(new { isDuplicate = false });

            try
            {
                string cleanEmail = email.Trim().ToLower();

                // 1. Check Accounts
                var dupAccount = await _context.Accounts
                    .FirstOrDefaultAsync(a => (a.Email != null && a.Email.ToLower() == cleanEmail) ||
                                              (a.AlternateEmailID != null && a.AlternateEmailID.ToLower() == cleanEmail));
                if (dupAccount != null)
                {
                    var ownerName = await _context.Users.Where(u => u.Id == dupAccount.AccountOwnerId).Select(u => u.fullName ?? u.FirstName).FirstOrDefaultAsync() ?? "Unassigned";
                    return Json(new { isDuplicate = true, recordType = "Account", recordName = dupAccount.AccountName, ownerName = ownerName });
                }

                // 2. Check Leads
                var dupLead = await _context.Leads
                    .FirstOrDefaultAsync(l => (l.EmailID != null && l.EmailID.ToLower() == cleanEmail) ||
                                              (l.AlternateEmailID != null && l.AlternateEmailID.ToLower() == cleanEmail));
                if (dupLead != null)
                {
                    var ownerName = await _context.Users.Where(u => u.Id == dupLead.LeadOwnerId).Select(u => u.fullName ?? u.FirstName).FirstOrDefaultAsync() ?? "Unassigned";
                    return Json(new { isDuplicate = true, recordType = "Lead", recordName = dupLead.LeadName, ownerName = ownerName });
                }

                return Json(new { isDuplicate = false });
            }
            catch (Exception ex)
            {
                return Json(new { isDuplicate = false, error = true, message = ex.Message });
            }
        }

        // ==========================================
        // BULK DELETE: AJAX POST (Safely deletes all linked data)
        // ==========================================
        [HttpPost]
        [IgnoreAntiforgeryToken] // 🚀 FIX: Prevents 400 Bad Request "Network Errors" in AJAX
        public async Task<IActionResult> BulkDelete([FromBody] List<int> ids)
        {
            if (ids == null || !ids.Any())
                return Json(new { success = false, message = "No records selected." });

            try
            {
                var accountsToDelete = await _context.Accounts.Where(a => ids.Contains(a.Id)).ToListAsync();
                if (!accountsToDelete.Any()) return Json(new { success = true });

                // 1. Delete Tasks linked to Accounts
                var relatedTasks = await _context.Set<Radar_CRM.Models.Task>()
                    .Where(t => t.AccountId != null && ids.Contains((int)t.AccountId)).ToListAsync();
                if (relatedTasks.Any()) _context.Set<Radar_CRM.Models.Task>().RemoveRange(relatedTasks);

                // 2. Delete Deals (AND any Notes attached to those Deals)
                var relatedDeals = await _context.Set<Deal>()
                    .Where(d => d.AccountId != null && ids.Contains((int)d.AccountId)).ToListAsync();
                if (relatedDeals.Any())
                {
                    var dealIds = relatedDeals.Select(d => d.Id).ToList();
                    if (_context.Note != null)
                    {
                        var dealNotes = await _context.Note.Where(n => n.DealId != null && dealIds.Contains((int)n.DealId)).ToListAsync();
                        if (dealNotes.Any()) _context.Note.RemoveRange(dealNotes);
                    }
                    _context.Set<Deal>().RemoveRange(relatedDeals);
                }

                // 3. Delete Leads (AND any Notes attached to those Leads)
                if (_context.Leads != null)
                {
                    var relatedLeads = await _context.Leads
                        .Where(l => l.AccountId != null && ids.Contains((int)l.AccountId)).ToListAsync();

                    if (relatedLeads.Any())
                    {
                        var leadIds = relatedLeads.Select(l => l.Id).ToList();
                        if (_context.Note != null)
                        {
                            var leadNotes = await _context.Note.Where(n => n.LeadId != null && leadIds.Contains((int)n.LeadId)).ToListAsync();
                            if (leadNotes.Any()) _context.Note.RemoveRange(leadNotes);
                        }
                        _context.Leads.RemoveRange(relatedLeads);
                    }
                }

                // 4. Delete Notes attached directly to the Account
                if (_context.Note != null)
                {
                    var relatedNotes = await _context.Note
                        .Where(n => n.AccountId != null && ids.Contains((int)n.AccountId)).ToListAsync();
                    if (relatedNotes.Any()) _context.Note.RemoveRange(relatedNotes);
                }

                // 5. Finally, remove the Accounts safely!
                _context.Accounts.RemoveRange(accountsToDelete);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // Returns clean JSON instead of crashing so your frontend can show the exact SQL error
                string errorMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "DB Error: " + errorMessage });
            }
        }
        // ==========================================
        // CHECK DUPLICATE PHONE VIA AJAX
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> CheckDuplicatePhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return Json(new { isDuplicate = false });

            try
            {
                // 1. Check Accounts
                var dupAccount = await _context.Accounts
                    .FirstOrDefaultAsync(a => (a.MobileNumber != null && a.MobileNumber.Contains(phone)) ||
                                              (a.AlternateMobile != null && a.AlternateMobile.Contains(phone)));
                if (dupAccount != null)
                {
                    var ownerName = await _context.Users.Where(u => u.Id == dupAccount.AccountOwnerId).Select(u => u.fullName ?? u.FirstName).FirstOrDefaultAsync() ?? "Unassigned";
                    return Json(new { isDuplicate = true, recordType = "Account", recordName = dupAccount.AccountName, ownerName = ownerName });
                }

                // 2. Check Leads
                var dupLead = await _context.Leads
                    .FirstOrDefaultAsync(l => (l.MobileNumber != null && l.MobileNumber.Contains(phone)) ||
                                              (l.AlternateMobile != null && l.AlternateMobile.Contains(phone)));
                if (dupLead != null)
                {
                    var ownerName = await _context.Users.Where(u => u.Id == dupLead.LeadOwnerId).Select(u => u.fullName ?? u.FirstName).FirstOrDefaultAsync() ?? "Unassigned";
                    return Json(new { isDuplicate = true, recordType = "Lead", recordName = dupLead.LeadName, ownerName = ownerName });
                }

                return Json(new { isDuplicate = false });
            }
            catch (Exception ex)
            {
                return Json(new { isDuplicate = false, error = true, message = ex.Message });
            }
        }


        // ==========================================
        // UPDATE TASK STATUS VIA AJAX
        // ==========================================
        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Route("Accounts/UpdateTaskStatus")]
        public async Task<IActionResult> UpdateTaskStatus(int id, string status)
        {
            try
            {
                var task = await _context.Set<Radar_CRM.Models.Task>().FindAsync(id);
                if (task == null) return Json(new { success = false, message = "Task not found" });

                task.Status = status;

                // Optionally log who modified it
                string currentUser = User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(User.Identity.Name)
                    ? User.Identity.Name : "System User";
                task.ModifiedBy = $"{currentUser} on {DateTime.Now.ToString("dd/MM/yy HH:mm")}";
                task.ModifiedTime = DateTime.Now;

                _context.Set<Radar_CRM.Models.Task>().Update(task);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> CheckMobileDuplicate(string mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile))
            {
                return Json(new { success = true, isDuplicate = false });
            }

            try
            {
                string cleanMobile = mobile.Trim();

                // REPLACE '_context.Leads' with the actual table you are checking (e.g., _context.Users or _context.Accounts)
                bool exists = await _context.Leads.AnyAsync(l => l.MobileNumber == cleanMobile);

                return Json(new { success = true, isDuplicate = exists });
            }
            catch (Exception ex)
            {
                // This will capture the exact SQL/Connection error on your live server
                string errorMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "DB Connection Error: " + errorMessage });
            }
        }

        // ==========================================
        // NOTIFICATION HELPER: Email & In-App Popup
        // ==========================================
        // 🚀 FIX: Changed List<string> to Dictionary<int, string> to hold both ID and Name
        private async Task NotifyNewOwnerAsync(string newOwnerId, Dictionary<int, string> assignedAccounts)
        {
            if (string.IsNullOrEmpty(newOwnerId) || assignedAccounts == null || !assignedAccounts.Any()) return;

            var newOwner = await _context.Users.FindAsync(newOwnerId);
            if (newOwner == null || string.IsNullOrEmpty(newOwner.Email)) return;

            string ownerName = newOwner.fullName ?? newOwner.FirstName ?? "Team Member";
            bool isBulk = assignedAccounts.Count > 1;

            string subject = isBulk
                ? $"Action Required: {assignedAccounts.Count} Accounts Assigned to You"
                : $"Action Required: Account '{assignedAccounts.Values.First()}' Assigned to You";

            // 🚀 FIX: Get the live Base URL of the application to build absolute links
            string baseUrl = $"{Request.Scheme}://{Request.Host}";

            // 🚀 FIX: Build Email Body with clickable links targeting the Edit page
            string accountListHtml = string.Join("", assignedAccounts.Select(acc =>
            {
                // This builds the full URL (e.g., https://yourdomain.com/Accounts/Edit/5)
                string linkUrl = Url.Action("Edit", "Accounts", new { id = acc.Key }, Request.Scheme);

                return $"<li style='margin-bottom: 8px;'><a href='{linkUrl}' style='color: #2563eb; text-decoration: none; font-weight: bold;'>{acc.Value}</a></li>";
            }));
            string body = $@"
                <div style='font-family: Arial, sans-serif; color: #333;'>
                    <h3>Hi {ownerName},</h3>
                    <p>You have been assigned as the new owner for the following {(isBulk ? "accounts" : "account")}:</p>
                    <ul>{accountListHtml}</ul>
                    <p>Please click the link(s) above or log in to the CRM to review your new assignments.</p>
                    <br/>
                    <p><small>This is an automated notification from Radar CRM.</small></p>
                </div>";

            // 2. Send Email via Resend API
            try
            {
                string apiKey = _configuration["Resend:ApiKey"];
                string senderEmail = "Radar CRM <no-reply@bjaincorp.com>";

                var emailPayload = new Dictionary<string, object>
                {
                    { "from", senderEmail },
                    { "to", new List<string> { newOwner.Email.Trim() } },
                    { "subject", subject },
                    { "html", body }
                };

                using (var client = new HttpClient())
                {
                    var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
                    requestMessage.Headers.Add("Authorization", $"Bearer {apiKey}");
                    requestMessage.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(emailPayload), System.Text.Encoding.UTF8, "application/json");

                    var response = await client.SendAsync(requestMessage);
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Resend API Error: {errorContent}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Email Dispatch Failed: {ex.Message}");
            }

            // 3. Create In-App Notification for Popup
            try
            {
                var systemTask = new Radar_CRM.Models.Task
                {
                    Subject = isBulk ? $"You have been assigned {assignedAccounts.Count} new accounts." : $"You are the new owner of {assignedAccounts.Values.First()}.",
                    Status = "Not Started",
                    Priority = "High",
                    TaskOwnerId = newOwnerId,
                    CreatedDateAndTime = DateTime.Now,
                    DueDate = DateTime.Now.AddDays(1)
                };

                _context.Set<Radar_CRM.Models.Task>().Add(systemTask);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"In-App Notification Failed: {ex.Message}");
            }
        }
        // ==========================================
        // UPLOAD FILE (With Duplicate Checking)
        // ==========================================
        [HttpPost]
        [DisableRequestSizeLimit]
        [RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = int.MaxValue)]
        public async Task<IActionResult> UploadFile(IFormFile uploadedFile)
        {
            if (uploadedFile == null || uploadedFile.Length == 0) return BadRequest("No file was uploaded.");

            var accountsToInsert = new List<Account>();
            // REMOVED: accountsToUpdate list
            var skippedRecords = new List<string>();
            int currentRow = 1;

            // Load existing data for ultra-fast checking
            var validUserIds = new HashSet<string>(_context.Users.Select(u => u.Id).ToList());

            var existingAccountsDb = await _context.Accounts
                .Where(a => !string.IsNullOrEmpty(a.ZohoRecordId))
                .AsNoTracking()
                .GroupBy(a => a.ZohoRecordId)
                .ToDictionaryAsync(g => g.Key, g => g.First());

            var existingMobiles = new HashSet<string>(await _context.Accounts.Where(a => !string.IsNullOrEmpty(a.MobileNumber)).Select(a => a.MobileNumber).ToListAsync());
            var existingEmails = new HashSet<string>(await _context.Accounts.Where(a => !string.IsNullOrEmpty(a.Email)).Select(a => a.Email.ToLower()).ToListAsync());

            // Helper to Standardize Country to INDIA
            string NormalizeCountry(string input)
            {
                if (string.IsNullOrWhiteSpace(input)) return null;
                string upper = input.Trim().ToUpper();
                if (upper.Contains("INDIA") || upper == "IND" || upper == "IN") return "INDIA";
                return upper;
            }

            try
            {
                using (var reader = new StreamReader(uploadedFile.OpenReadStream()))
                {
                    var headerLine = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(headerLine)) return BadRequest("Empty CSV");

                    // Standardize headers
                    var headers = ParseCsvLine(headerLine).Select(h => h.Trim().ToLower().Replace(" ", "")).ToList();

                    string GetValSafe(string[] vals, string colName)
                    {
                        var idx = headers.IndexOf(colName.ToLower().Replace(" ", ""));
                        return idx >= 0 && idx < vals.Length ? vals[idx]?.Trim() ?? "" : "";
                    }

                    decimal? GetDecimalSafe(string[] vals, string colName)
                    {
                        string raw = GetValSafe(vals, colName);
                        if (string.IsNullOrWhiteSpace(raw)) return null;
                        string clean = new string(raw.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());
                        return decimal.TryParse(clean, out decimal result) ? result : null;
                    }

                    while (!reader.EndOfStream)
                    {
                        currentRow++;
                        var line = await reader.ReadLineAsync();
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var values = ParseCsvLine(line);
                        string zohoRecordId = GetValSafe(values, "RecordId");
                        string mobile = GetValSafe(values, "MobileNumber");
                        string email = GetValSafe(values, "Email")?.ToLower();

                        // 🚀 CHANGED: INSERT-ONLY LOGIC
                        bool isZohoDup = !string.IsNullOrEmpty(zohoRecordId) && existingAccountsDb.ContainsKey(zohoRecordId);
                        bool isMobileDup = !string.IsNullOrEmpty(mobile) && existingMobiles.Contains(mobile);
                        bool isEmailDup = !string.IsNullOrEmpty(email) && existingEmails.Contains(email);

                        // If it exists ANYWHERE in the database, skip this row completely!
                        if (isZohoDup || isMobileDup || isEmailDup)
                        {
                            skippedRecords.Add($"Row {currentRow}: Skipped (Duplicate Record found)");
                            continue;
                        }

                        // If we reach here, it is 100% brand new.
                        Account acc = new Account();
                        if (!string.IsNullOrEmpty(mobile)) existingMobiles.Add(mobile);
                        if (!string.IsNullOrEmpty(email)) existingEmails.Add(email);

                        // 🚀 MAP ALL FIELDS FOR THE NEW RECORD
                        string rawOwnerId = GetValSafe(values, "AccountsOwner.id");
                        string rawCoOwnerId = GetValSafe(values, "Co-Owner.id");

                        acc.ZohoRecordId = zohoRecordId;
                        acc.AccountOwnerId = validUserIds.Contains(rawOwnerId) ? rawOwnerId : null;
                        acc.CoOwnerId = validUserIds.Contains(rawCoOwnerId) ? rawCoOwnerId : null;
                        acc.AccountName = GetValSafe(values, "AccountName");
                        acc.Email = GetValSafe(values, "Email");
                        acc.AlternateEmailID = GetValSafe(values, "AlternateEmailID");
                        acc.MobileNumber = mobile;
                        acc.AlternateMobile = GetValSafe(values, "AlternateMobile");
                        acc.DataSource = GetValSafe(values, "DataSources");
                        acc.Description = GetValSafe(values, "Description");
                        acc.CurrentStatus = GetValSafe(values, "CurrentStatus");
                        acc.AccountType = GetValSafe(values, "AccountType");
                        acc.DateOfEntry = DateTime.TryParse(GetValSafe(values, "DateofEntry"), out DateTime doe) ? doe : DateTime.Now;
                        acc.CreatedDateAndTime = DateTime.TryParse(GetValSafe(values, "Created Time"), out DateTime cdt) ? cdt : DateTime.Now;
                        acc.ModifiedDateAndTime = DateTime.TryParse(GetValSafe(values, "Modified Time"), out DateTime mdt) ? mdt : DateTime.Now;
                        acc.CreatedBy = GetValSafe(values, "CreatedBy");
                        acc.ModifiedBy = GetValSafe(values, "ModifiedBy");
                        acc.IsDuplicated = bool.TryParse(GetValSafe(values, "IsDuplicated"), out bool isDup) ? isDup : false;

                        // --- Address 1 ---
                        acc.Addr1_Country = NormalizeCountry(GetValSafe(values, "Address1-Country/Region"));
                        acc.Addr1_FlatHouse = GetValSafe(values, "Address1-Flat/HouseNo./Building/ApartmentName");
                        acc.Addr1_Street = GetValSafe(values, "Address1-StreetAddress");
                        acc.Addr1_City = GetValSafe(values, "Address1-City");
                        acc.Addr1_State = GetValSafe(values, "Address1-State/Province");
                        acc.Addr1_Zip = GetValSafe(values, "Address1-Zip/PostalCode");
                        acc.Addr1_Latitude = GetValSafe(values, "Address1-Latitude");
                        acc.Addr1_Longitude = GetValSafe(values, "Address1-Longitude");

                        // --- Address 2 ---
                        acc.Addr2_Country = NormalizeCountry(GetValSafe(values, "Address2-Country/Region"));
                        acc.Addr2_FlatHouse = GetValSafe(values, "Address2-Flat/HouseNo./Building/ApartmentName");
                        acc.Addr2_Street = GetValSafe(values, "Address2-StreetAddress");
                        acc.Addr2_City = GetValSafe(values, "Address2-City");
                        acc.Addr2_State = GetValSafe(values, "Address2-State/Province");
                        acc.Addr2_Zip = GetValSafe(values, "Address2-Zip/PostalCode");
                        acc.Addr2_Latitude = GetValSafe(values, "Address2-Latitude");
                        acc.Addr2_Longitude = GetValSafe(values, "Address2-Longitude");

                        // --- Contact Persons ---
                        acc.ContactPersonName = GetValSafe(values, "ContactPersonName");
                        acc.ContactPerson1 = GetValSafe(values, "ContactPersonName1");
                        acc.ContactPerson2 = GetValSafe(values, "ContactPersonName2");
                        acc.ContactPerson3 = GetValSafe(values, "ContactPersonName3");
                        acc.Contact1Phone = GetValSafe(values, "Contact1PhoneNumber");
                        acc.Contact2Phone = GetValSafe(values, "Contact2PhoneNumber");
                        acc.Contact3Phone = GetValSafe(values, "Contact3PhoneNumber");

                        // --- Professional Details ---
                        acc.GroupName = GetValSafe(values, "GroupsName");
                        acc.SeminarName = GetValSafe(values, "SeminarName");
                        acc.LeadStatus = GetValSafe(values, "LeadStatus");
                        acc.ProfilePendingReason = GetValSafe(values, "ProfilePendingReason");
                        acc.QualificationStatus = GetValSafe(values, "QualifiactionStatus");
                        acc.IsHomeopathicDoctor = GetValSafe(values, "AreYouaHomeopathicDoctor");
                        acc.ClinicType = GetValSafe(values, "ClinicType");
                        acc.YearsOfPractice = int.TryParse(GetValSafe(values, "YearsofParctice"), out int yop) ? yop : null;
                        acc.Qualification = GetValSafe(values, "Qualification");
                        acc.YearOfPassing = GetValSafe(values, "Yearofpassing");
                        acc.AveragePatientFee = GetDecimalSafe(values, "AveragePatientFee");
                        acc.DateOfBirth = DateTime.TryParse(GetValSafe(values, "DateofBirth"), out DateTime dob) ? dob : null;
                        acc.WorkType = GetValSafe(values, "WorkType");
                        acc.HasComputer = GetValSafe(values, "HavingComputer/Laptop");
                        acc.PatientsPerDay = int.TryParse(GetValSafe(values, "PatientsperDay"), out int ppd) ? ppd : null;
                        acc.CollegeName = GetValSafe(values, "CollegeName");
                        acc.TotalExperience = int.TryParse(GetValSafe(values, "Totalexperience"), out int te) ? te : null;
                        acc.NumberOfClinics = int.TryParse(GetValSafe(values, "NumberofClinics"), out int noc) ? noc : null;
                        acc.Age = int.TryParse(GetValSafe(values, "Age"), out int age) ? age : null;
                        acc.ProfileCompletionPercentage = int.TryParse(GetValSafe(values, "Profilecompletion(%)"), out int pcp) ? pcp : null;
                        acc.Profilestatus = GetValSafe(values, "ProfileStatus");
                        acc.ProfileRate = int.TryParse(GetValSafe(values, "ProfileRate"), out int pr) ? pr : null;
                        acc.ReferralSource = GetValSafe(values, "ReferralSource");

                        // --- Software & Purchases ---
                        acc.CurrentlyUsingSoftware = GetValSafe(values, "CurrentlyUsingSoftware");
                        acc.CurrentSoftwareName = GetValSafe(values, "CurrentsoftwareName");
                        acc.RadarOpusLicenseNo = GetValSafe(values, "RadarOpusLicenseno");
                        acc.RadarOpusVersion = GetValSafe(values, "RadarOpusversion");
                        acc.ProductPurchased = GetValSafe(values, "Product/PackagePurchased");
                        acc.PurchaseDate = DateTime.TryParse(GetValSafe(values, "PurchaseDate"), out DateTime pd) ? pd : null;
                        acc.PurchaseValue = GetDecimalSafe(values, "PurchaseValue(â‚¹)");
                        acc.PaymentType = GetValSafe(values, "PaymentType");
                        acc.PaymentStatus = GetValSafe(values, "PaymentStatus");
                        acc.InvoiceNumber = GetValSafe(values, "InvoiceNumber");

                        // 🚀 --- NEW: AD & CAMPAIGN TRACKING --- 🚀
                        acc.MetaCampaignName = GetValSafe(values, "MetaCampaignName");
                        acc.SocialLeadId = GetValSafe(values, "SocialLeadID");
                        acc.LeadStage = GetValSafe(values, "LeadStage");
                        acc.OldLeadStatus = GetValSafe(values, "OldLead_Status");
                        acc.Gclid = GetValSafe(values, "GCLID");
                        acc.Zcampaignid = GetValSafe(values, "ZCAMPAIGNID");
                        acc.Adgroupid = GetValSafe(values, "ADGROUPID");
                        acc.Adid = GetValSafe(values, "ADID");
                        acc.Keywordid = GetValSafe(values, "KEYWORDID");
                        acc.Keyword = GetValSafe(values, "Keyword");
                        acc.ClickType = GetValSafe(values, "ClickType");
                        acc.DeviceType = GetValSafe(values, "DeviceType");
                        acc.AdNetwork = GetValSafe(values, "AdNetwork");
                        acc.SearchPartnerNetwork = GetValSafe(values, "SearchPartnerNetwork");
                        acc.AdCampaignName = GetValSafe(values, "AdCampaignName");
                        acc.AdGroupName = GetValSafe(values, "AdGroupName");
                        acc.Ad = GetValSafe(values, "Ad");
                        acc.Gadconfigid = GetValSafe(values, "GADCONFIGID");
                        acc.AdClickDate = DateTime.TryParse(GetValSafe(values, "AdClickDate"), out DateTime acd) ? acd : null;
                        acc.CostPerClick = GetDecimalSafe(values, "CostperClick");
                        acc.CostPerConversion = GetDecimalSafe(values, "CostperConversion");
                        acc.ConversionExportedOn = DateTime.TryParse(GetValSafe(values, "ConversionExportedOn"), out DateTime ceo) ? ceo : null;
                        acc.ConversionExportStatus = GetValSafe(values, "ConversionExportStatus");
                        acc.ReasonForConversionFailure = GetValSafe(values, "ReasonforConversionFailure");

                        // Just add to Insert list (no Update list anymore)
                        accountsToInsert.Add(acc);
                    }
                }

                _context.ChangeTracker.AutoDetectChangesEnabled = false;

                // 🚀 Execute Inserts Only
                if (accountsToInsert.Any()) await _context.Accounts.AddRangeAsync(accountsToInsert);

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Failed at Row {currentRow} -> {ex.Message}");
            }
            finally { _context.ChangeTracker.AutoDetectChangesEnabled = true; }

            return Json(new
            {
                success = true,
                insertedCount = accountsToInsert.Count,
                updatedCount = 0, // Hardcoded to 0 since updates are disabled
                skippedCount = skippedRecords.Count
            });
        }





        private string GetVal(string[] values, int index)
        {
            if (index < values.Length) return values[index]?.Trim() ?? "";
            return "";
        }

        private string[] ParseCsvLine(string line)
        {
            var result = new System.Collections.Generic.List<string>();
            bool inQuotes = false;
            var currentField = new System.Text.StringBuilder();

            foreach (char c in line)
            {
                if (c == '\"') inQuotes = !inQuotes;
                else if (c == ',' && !inQuotes)
                {
                    result.Add(currentField.ToString());
                    currentField.Clear();
                }
                else currentField.Append(c);
            }
            result.Add(currentField.ToString());
            return result.ToArray();
        }

        private bool AccountExists(int id)
        {
            return _context.Accounts.Any(e => e.Id == id);
        }


        // ==========================================
        // QUICK INFO AUTO-SAVE: AJAX POST
        // ==========================================
        // ==========================================
        // QUICK INFO AUTO-SAVE: AJAX POST
        // ==========================================
        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Route("Accounts/UpdateQuickInfo")]
        public async Task<IActionResult> UpdateQuickInfo(int id, string field, string newValue)
        {
            var account = await _context.Accounts.FindAsync(id);
            if (account == null) return Json(new { success = false, message = "Account not found" });

            // Safely extract the value
            string trimmedValue = newValue?.Trim() ?? "";

            try
            {
                // 🚀 FIX: Use Reflection to dynamically update ANY field passed from the frontend
                var propertyInfo = typeof(Account).GetProperty(field, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (propertyInfo != null)
                {
                    Type t = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;
                    object safeValue = null;

                    // 🚀 FIX: Prevent Null Reference Errors on Required Fields (Like Email)
                    if (t == typeof(string))
                    {
                        safeValue = trimmedValue == "-None-" ? "" : trimmedValue;
                    }
                    else if (!string.IsNullOrWhiteSpace(trimmedValue) && trimmedValue != "-None-")
                    {
                        if (t == typeof(DateTime))
                        {
                            if (DateTime.TryParse(trimmedValue, out DateTime d)) safeValue = d;
                        }
                        else
                        {
                            safeValue = Convert.ChangeType(trimmedValue, t);
                        }
                    }

                    // Apply the value to the model
                    propertyInfo.SetValue(account, safeValue, null);
                }
                else
                {
                    return Json(new { success = false, message = "Invalid field mapping: " + field });
                }

                string currentUser = User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(User.Identity.Name)
                    ? User.Identity.Name
                    : "System User";

                account.ModifiedBy = $"{currentUser} on {DateTime.Now.ToString("dd/MM/yy HH:mm")}";

                account.ModifiedDateAndTime = DateTime.Now;

                // Explicitly tell Entity Framework to update the record
                _context.Update(account);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // ⚠️️ NOTE: If the email already belongs to someone else, SQL will reject it here
                string trueError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = trueError });
            }
        }

        [HttpGet]
        [Route("api/timeline/{entityType}/{entityId}")]
        public async Task<IActionResult> GetUniversalTimeline(string entityType, int entityId)
        {
            var events = await _context.TimelineEvents
                .Where(t => t.EntityType == entityType && t.EntityId == entityId)
                .OrderByDescending(t => t.CreatedDate)
                .Select(t => new
                {
                    id = t.Id,
                    eventType = t.EventType,
                    actionDescription = t.ActionDescription,
                    createdByName = t.CreatedByName ?? "System User",
                    createdDate = t.CreatedDate.ToString("MMM dd, yyyy - h:mm tt")
                })
                .ToListAsync();

            return Json(events);
        }

    }
}