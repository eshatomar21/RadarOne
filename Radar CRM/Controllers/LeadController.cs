using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Radar_CRM.Data;
using Radar_CRM.Migrations;
using Microsoft.Extensions.Configuration;
using Radar_CRM.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using static Radar_CRM.Controllers.AccountsController;
using CrmTaskModel = Radar_CRM.Models.Task;
using Task = System.Threading.Tasks.Task;

namespace Radar_CRM.Controllers
{
    public class LeadsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        // 🚀 FIX: Add 'IConfiguration configuration' inside the parentheses here
        public LeadsController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration; // Now it knows where this comes from!
        }

        // ==========================================
        // GET: Leads/Index (Paginated, Sorted, Filtered)
        // ==========================================
        public async Task<IActionResult> Index(int page = 1, string search = "", string sortCol = "Id", string sortDir = "desc", string advancedFilters = "")
        {
            if (!User.Identity.IsAuthenticated) return RedirectToAction("Login", "Users");

            string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentUser = await _context.Users.FindAsync(currentUserId);

            if (currentUser == null) return RedirectToAction("Login", "Users");

            int pageSize = 100;

            // 🚀 Eager-load relational User objects for Leads
            var query = _context.Leads
                .Include(l => l.LeadOwner)
                .Include(l => l.CoOwner)
                .Include(l => l.AccountOwner)
                .Include(l => l.DemoOwner)
                .AsQueryable();

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
                query = query.Where(l => visibleUserIds.Contains(l.LeadOwnerId));
            }

            // 🚀 BULLETPROOF FILTERING ENGINE (ENTIRE DB)
            // 🚀 BULLETPROOF FILTERING ENGINE (ENTIRE DB)
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
                    var filters = System.Text.Json.JsonSerializer.Deserialize<List<LeadFilterCriteria>>(jsonString, options);

                    if (filters != null && filters.Any())
                    {
                        // 1. 🚀 INTERCEPT RELATED TASK FILTERS
                        var taskFilters = filters.Where(f => f.ColumnName == "RelatedTasksPresence" || f.ColumnName.StartsWith("Task.")).ToList();

                        // Remove them from the main list so the Lead-reflection engine ignores them
                        filters.RemoveAll(f => taskFilters.Contains(f));

                        // =======================================================
                        // 2. PROCESS STANDARD LEAD FILTERS (Existing Logic)
                        // =======================================================
                        var parameter = System.Linq.Expressions.Expression.Parameter(typeof(Lead), "l");
                        System.Linq.Expressions.Expression combinedPredicate = null;

                        foreach (var f in filters)
                        {
                            if (string.IsNullOrWhiteSpace(f.Value) && f.Condition != "is_empty" && f.Condition != "is_not_empty") continue;

                            string dbColName = f.ColumnName;

                            var propertyInfo = typeof(Lead).GetProperty(dbColName, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            if (propertyInfo != null && propertyInfo.PropertyType.IsClass && propertyInfo.PropertyType != typeof(string))
                            {
                                var idProp = typeof(Lead).GetProperty(dbColName + "Id", System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
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
                            // OWNER / USER MAPPING & AUDIT FIELDS
                            else if (dbColName.Contains("Owner", StringComparison.OrdinalIgnoreCase) || dbColName == "CreatedBy" || dbColName == "ModifiedBy")
                            {
                                // 🚀 The UI multi-select passes User IDs. Split them into a list.
                                var userIds = safeValue.Split(',').Select(id => id.Trim()).ToList();

                                // SCENARIO 1: The filter targets the exact ID column (e.g. "LeadOwnerId")
                                if (dbColName.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
                                {
                                    var listExpr = System.Linq.Expressions.Expression.Constant(userIds);
                                    var listContainsMethod = typeof(List<string>).GetMethod("Contains", new[] { typeof(string) });

                                    if (f.Condition == "is" || f.Condition == "in" || f.Condition == "contains")
                                        conditionExpr = System.Linq.Expressions.Expression.Call(listExpr, listContainsMethod, propExpr);
                                    else if (f.Condition == "is_not" || f.Condition == "does_not_contain")
                                        conditionExpr = System.Linq.Expressions.Expression.Not(System.Linq.Expressions.Expression.Call(listExpr, listContainsMethod, propExpr));
                                    else if (f.Condition == "is_empty")
                                        conditionExpr = System.Linq.Expressions.Expression.Call(typeof(string).GetMethod("IsNullOrEmpty"), propExpr);
                                    else if (f.Condition == "is_not_empty")
                                        conditionExpr = System.Linq.Expressions.Expression.Not(System.Linq.Expressions.Expression.Call(typeof(string).GetMethod("IsNullOrEmpty"), propExpr));
                                }
                                // SCENARIO 2: The filter targets the TEXT Name column (e.g., "LeadOwner")
                                else
                                {
                                    // 🚀 We must translate the User IDs from the dropdown into User Names from the database!
                                    var userNames = _context.Users
                                        .Where(u => userIds.Contains(u.Id))
                                        .Select(u => u.fullName ?? u.FirstName)
                                        .ToList();

                                    if (userNames.Any())
                                    {
                                        var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes);
                                        var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) });
                                        var notNullProp = System.Linq.Expressions.Expression.Coalesce(propExpr, System.Linq.Expressions.Expression.Constant(""));
                                        var lowerProp = System.Linq.Expressions.Expression.Call(notNullProp, toLowerMethod);

                                        System.Linq.Expressions.Expression nameConditions = null;

                                        // Build an OR condition for every selected name
                                        foreach (var name in userNames.Where(n => !string.IsNullOrWhiteSpace(n)))
                                        {
                                            var lowerVal = System.Linq.Expressions.Expression.Constant(name.Trim().ToLower());
                                            var containsExpr = System.Linq.Expressions.Expression.Call(lowerProp, containsMethod, lowerVal);
                                            nameConditions = nameConditions == null ? containsExpr : System.Linq.Expressions.Expression.OrElse(nameConditions, containsExpr);
                                        }

                                        if (f.Condition == "is_not" || f.Condition == "does_not_contain")
                                            conditionExpr = System.Linq.Expressions.Expression.Not(nameConditions);
                                        else
                                            conditionExpr = nameConditions;
                                    }
                                    else if (f.Condition == "is_empty")
                                    {
                                        conditionExpr = System.Linq.Expressions.Expression.Call(typeof(string).GetMethod("IsNullOrEmpty"), propExpr);
                                    }
                                    else if (f.Condition == "is_not_empty")
                                    {
                                        conditionExpr = System.Linq.Expressions.Expression.Not(System.Linq.Expressions.Expression.Call(typeof(string).GetMethod("IsNullOrEmpty"), propExpr));
                                    }
                                }
                            }
                            // REGULAR STRINGS
                            else if (propertyInfo.PropertyType == typeof(string))
                            {
                                var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes);
                                var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) });
                                var notNullProp = System.Linq.Expressions.Expression.Coalesce(propExpr, System.Linq.Expressions.Expression.Constant(""));
                                var lowerProp = System.Linq.Expressions.Expression.Call(notNullProp, toLowerMethod);
                                var lowerVal = System.Linq.Expressions.Expression.Constant(safeValue.ToLower());

                                // 🚀 FIX: Automatically support comma-separated Multi-Selects for standard strings too
                                if (f.Condition == "is")
                                {
                                    if (safeValue.Contains(","))
                                    {
                                        var valuesList = safeValue.Split(',').Select(v => v.Trim().ToLower()).ToList();
                                        var listExpr = System.Linq.Expressions.Expression.Constant(valuesList);
                                        var listContainsMethod = typeof(List<string>).GetMethod("Contains", new[] { typeof(string) });
                                        conditionExpr = System.Linq.Expressions.Expression.Call(listExpr, listContainsMethod, lowerProp);
                                    }
                                    else conditionExpr = System.Linq.Expressions.Expression.Equal(lowerProp, lowerVal);
                                }
                                else if (f.Condition == "is_not")
                                {
                                    if (safeValue.Contains(","))
                                    {
                                        var valuesList = safeValue.Split(',').Select(v => v.Trim().ToLower()).ToList();
                                        var listExpr = System.Linq.Expressions.Expression.Constant(valuesList);
                                        var listContainsMethod = typeof(List<string>).GetMethod("Contains", new[] { typeof(string) });
                                        conditionExpr = System.Linq.Expressions.Expression.Not(System.Linq.Expressions.Expression.Call(listExpr, listContainsMethod, lowerProp));
                                    }
                                    else conditionExpr = System.Linq.Expressions.Expression.NotEqual(lowerProp, lowerVal);
                                }
                                else if (f.Condition == "contains") conditionExpr = System.Linq.Expressions.Expression.Call(lowerProp, containsMethod, lowerVal);
                                else if (f.Condition == "does_not_contain") conditionExpr = System.Linq.Expressions.Expression.Not(System.Linq.Expressions.Expression.Call(lowerProp, containsMethod, lowerVal));
                                else if (f.Condition == "starts_with") conditionExpr = System.Linq.Expressions.Expression.Call(lowerProp, typeof(string).GetMethod("StartsWith", new[] { typeof(string) }), lowerVal);
                                else if (f.Condition == "ends_with") conditionExpr = System.Linq.Expressions.Expression.Call(lowerProp, typeof(string).GetMethod("EndsWith", new[] { typeof(string) }), lowerVal);
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
                            var lambda = System.Linq.Expressions.Expression.Lambda<Func<Lead, bool>>(combinedPredicate, parameter);
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

                            var lParam = System.Linq.Expressions.Expression.Parameter(typeof(Lead), "l");
                            var tasksProp = System.Linq.Expressions.Expression.Property(lParam, "Tasks");
                            var tParam = System.Linq.Expressions.Expression.Parameter(typeof(CrmTaskModel), "t");

                            // 🚀 NEW REQUIREMENT: Base condition -> Task MUST NOT be "Completed"
                            var statusProp = System.Linq.Expressions.Expression.Property(tParam, "Status");
                            var completedConst = System.Linq.Expressions.Expression.Constant("Completed");
                            System.Linq.Expressions.Expression tCombined = System.Linq.Expressions.Expression.NotEqual(statusProp, completedConst);

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

                                    if (cond == "before")
                                        tCondExpr = System.Linq.Expressions.Expression.LessThan(efCall, d1Const);
                                    else if (cond == "after")
                                        tCondExpr = System.Linq.Expressions.Expression.GreaterThan(efCall, d1EndConst);
                                    else if (cond == "between")
                                        tCondExpr = System.Linq.Expressions.Expression.AndAlso(System.Linq.Expressions.Expression.GreaterThanOrEqual(efCall, d1Const), System.Linq.Expressions.Expression.LessThanOrEqual(efCall, d2EndConst));
                                    else if (cond == "not between")
                                        tCondExpr = System.Linq.Expressions.Expression.OrElse(System.Linq.Expressions.Expression.LessThan(efCall, d1Const), System.Linq.Expressions.Expression.GreaterThan(efCall, d2EndConst));
                                    else // "On"
                                        tCondExpr = System.Linq.Expressions.Expression.AndAlso(System.Linq.Expressions.Expression.GreaterThanOrEqual(efCall, d1Const), System.Linq.Expressions.Expression.LessThan(efCall, d1NextConst));
                                }

                                if (tCondExpr != null)
                                {
                                    // 🚀 Chain new conditions onto the base (Status != Completed) condition using AND
                                    tCombined = System.Linq.Expressions.Expression.AndAlso(tCombined, tCondExpr);
                                }
                            }

                            // 🚀 Generate the final Any() check
                            var taskLambda = System.Linq.Expressions.Expression.Lambda<Func<CrmTaskModel, bool>>(tCombined, tParam);
                            var anyMethod = typeof(Enumerable).GetMethods().First(m => m.Name == "Any" && m.GetParameters().Length == 2).MakeGenericMethod(typeof(CrmTaskModel));
                            var anyCall = System.Linq.Expressions.Expression.Call(anyMethod, tasksProp, taskLambda);

                            if (isWithout)
                            {
                                // WITHOUT ACTIVE TASKS
                                var notAnyCall = System.Linq.Expressions.Expression.Not(anyCall);
                                var leadLambda = System.Linq.Expressions.Expression.Lambda<Func<Lead, bool>>(notAnyCall, lParam);
                                query = query.Where(leadLambda);
                            }
                            else
                            {
                                // WITH ACTIVE TASKS
                                var leadLambda = System.Linq.Expressions.Expression.Lambda<Func<Lead, bool>>(anyCall, lParam);
                                query = query.Where(leadLambda);
                            }
                        }
                    } // <-- MISSING BRACE 1: Closes if (filters != null && filters.Any())
                } // <-- MISSING BRACE 2: Closes try {
                catch (Exception ex)
                {
                    // 🚀 CRITICAL FIX: Print exactly why the filter fails so you can see it in your Visual Studio Output/Console
                    Console.WriteLine("\n=========================================");
                    Console.WriteLine("FILTER ENGINE CRASH LOG:");
                    Console.WriteLine(ex.Message);
                    Console.WriteLine("=========================================\n");
                }
            } // <-- MISSING BRACE 3: Closes if (!string.IsNullOrWhiteSpace(advancedFilters))

            if (!string.IsNullOrEmpty(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(l =>
                    (l.LeadName != null && l.LeadName.ToLower().Contains(searchLower)) ||
                    (l.MobileNumber != null && l.MobileNumber.Contains(search)) ||
                    (l.EmailID != null && l.EmailID.ToLower().Contains(searchLower))
                );
            }

            if (sortDir == "desc")
            {
                query = sortCol switch
                {
                    "LeadName" => query.OrderByDescending(l => l.LeadName),
                    "MobileNumber" => query.OrderByDescending(l => l.MobileNumber),
                    "CurrentStatus" => query.OrderByDescending(l => l.CurrentStatus),
                    _ => query.OrderByDescending(l => l.Id)
                };
            }
            else
            {
                query = sortCol switch
                {
                    "LeadName" => query.OrderBy(l => l.LeadName),
                    "MobileNumber" => query.OrderBy(l => l.MobileNumber),
                    "CurrentStatus" => query.OrderBy(l => l.CurrentStatus),
                    _ => query.OrderBy(l => l.Id)
                };
            }

            // Safely calculate total records
            var totalRecords = await query.CountAsync();

            if (page < 1) page = 1;
            int totalPagesCalc = (int)Math.Ceiling(totalRecords / (double)pageSize);
            if (totalPagesCalc == 0) totalPagesCalc = 1;
            if (page > totalPagesCalc) page = totalPagesCalc;

            var leads = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPagesCalc;
            ViewBag.TotalRecords = totalRecords;
            ViewBag.AllMatchingIds = await query.Select(l => l.Id).ToListAsync();
            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            // 🚀 ADD THIS LINE TO FIX THE CRASH:
            ViewBag.VendorsList = new SelectList(_context.Vendors, "Id", "VendorName");

            // 🚀 NEW: Safely get note counts only for the loaded leads
            var leadIds = leads.Select(l => l.Id).ToList();
            var noteCounts = await _context.Note
                .Where(n => n.LeadId != null && leadIds.Contains(n.LeadId.Value))
                .GroupBy(n => n.LeadId.Value)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            ViewBag.NoteCounts = noteCounts; // Send dictionary to the view

            // 1. Get the IDs of the Leads currently being displayed
            var currentLeadIds = leads.Select(l => l.Id).ToList();

            // 2. Fetch upcoming tasks strictly for Leads
            var upcomingTasks = _context.Tasks
                .Where(t => t.LeadId.HasValue && currentLeadIds.Contains(t.LeadId.Value) && t.Status != "Completed")
                .ToList()
                .GroupBy(t => t.LeadId.Value)
                .Select(g => g.OrderBy(t => t.DueDate).FirstOrDefault())
                .ToList();

            // 3. Map to Dictionary
            var leadTasksDict = new Dictionary<int, dynamic>();
            foreach (var task in upcomingTasks)
            {
                if (task != null && task.LeadId.HasValue)
                {
                    leadTasksDict[task.LeadId.Value] = new
                    {
                        Subject = task.Subject,
                        DueDate = task.DueDate
                    };
                }
            }

            // 4. Send to View
            ViewBag.LeadTasks = leadTasksDict;

            return View(leads);
        }


        // ==========================================
        // BULK UPDATE FOR LEADS: AJAX POST
        // ==========================================
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> BulkUpdate([FromBody] BulkUpdateRequest request)
        {
            if (request == null || request.Ids == null || !request.Ids.Any() || string.IsNullOrEmpty(request.FieldName))
            {
                return Json(new { success = false, message = "Invalid selection or field." });
            }

            try
            {
                var leadsToUpdate = await _context.Leads.Where(l => request.Ids.Contains(l.Id)).ToListAsync();

                string targetFieldName = request.FieldName;

                // 🚀 FIX: If user selects LeadOwner, CoOwner, AccountOwner, or DemoOwner, map it to the Foreign Key ID property!
                if (targetFieldName.Equals("LeadOwner", StringComparison.OrdinalIgnoreCase) ||
                    targetFieldName.Equals("CoOwner", StringComparison.OrdinalIgnoreCase) ||
                    targetFieldName.Equals("AccountOwner", StringComparison.OrdinalIgnoreCase) ||
                    targetFieldName.Equals("DemoOwner", StringComparison.OrdinalIgnoreCase))
                {
                    targetFieldName += "Id";
                }

                var propertyInfo = typeof(Lead).GetProperty(targetFieldName,
                    System.Reflection.BindingFlags.IgnoreCase |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance);

                if (propertyInfo == null)
                    return Json(new { success = false, message = $"Field '{targetFieldName}' not found on Lead entity." });

                foreach (var lead in leadsToUpdate)
                {
                    object safeValue = null;
                    Type targetType = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;

                    if (!string.IsNullOrWhiteSpace(request.NewValue))
                    {
                        if (targetType == typeof(DateTime))
                        {
                            if (DateTime.TryParse(request.NewValue, out DateTime parsedDate)) safeValue = parsedDate;
                        }
                        else if (targetType == typeof(DateOnly))
                        {
                            if (DateOnly.TryParse(request.NewValue, out DateOnly parsedDateOnly)) safeValue = parsedDateOnly;
                            else if (DateTime.TryParse(request.NewValue, out DateTime dt)) safeValue = DateOnly.FromDateTime(dt);
                        }
                        else if (targetType == typeof(bool))
                        {
                            if (bool.TryParse(request.NewValue, out bool parsedBool)) safeValue = parsedBool;
                        }
                        else
                        {
                            safeValue = Convert.ChangeType(request.NewValue, targetType);
                        }
                    }

                    propertyInfo.SetValue(lead, safeValue, null);
                }

                _context.UpdateRange(leadsToUpdate);
                await _context.SaveChangesAsync();

                // =========================================================
                // 🚀 NEW: TRANSFER TASKS IF LEAD OWNER WAS CHANGED IN BULK
                // =========================================================
                if (targetFieldName.Equals("LeadOwnerId", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(request.NewValue))
                {
                    // 1. FETCH NEW OWNER'S NAME (Crucial for the UI)
                    string newOwnerName = "System User";
                    var newOwnerUser = await _context.Users.FindAsync(request.NewValue);
                    if (newOwnerUser != null)
                    {
                        newOwnerName = newOwnerUser.fullName ?? newOwnerUser.FirstName ?? "System User";
                    }

                    // 2. Fetch all incomplete tasks related to ALL selected Leads
                    var relatedTasks = await _context.Set<Radar_CRM.Models.Task>()
                        .Where(t => t.LeadId.HasValue && request.Ids.Contains(t.LeadId.Value) && t.Status != "Completed")
                        .ToListAsync();

                    if (relatedTasks.Any())
                    {
                        string currentUser = User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(User.Identity.Name) ? User.Identity.Name : "System Bulk Update";

                        foreach (var task in relatedTasks)
                        {
                            // Update both the ID and the Text Field for the UI
                            task.TaskOwnerId = request.NewValue;
                            task.TaskOwner = newOwnerName;

                            task.ModifiedBy = $"{currentUser} (System Bulk Transfer) on {DateTime.Now.ToString("MMM dd, yyyy - hh:mm tt")}";
                            task.ModifiedTime = DateTime.Now;

                            // 🚀 CRITICAL FIX: Force Entity Framework to track this modification
                            _context.Entry(task).State = EntityState.Modified;
                        }

                        _context.Set<Radar_CRM.Models.Task>().UpdateRange(relatedTasks);
                        await _context.SaveChangesAsync();
                    }

                    // 🚀 NEW: SEND EMAIL NOTIFICATION TO THE NEW OWNER FOR BULK TRANSFER
                    var assignedLeads = leadsToUpdate.ToDictionary(l => l.Id, l => l.LeadName ?? "Unknown Lead");
                    await NotifyNewOwnerAsync(request.NewValue, assignedLeads);
                }
                // =========================================================

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                string message = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = message });
            }
        }

        public class BulkUpdateRequest
        {
            public List<int> Ids { get; set; }
            public string FieldName { get; set; }
            public string NewValue { get; set; }
        }

        // 🚀 ADD THIS NEW UNIQUE CLASS HERE:
        public class LeadFilterCriteria
        {
            public string LogicalOperator { get; set; }
            public string ColumnName { get; set; }
            public string Condition { get; set; }
            public string Value { get; set; }
            public bool IsDate { get; set; }
        }

        // ==========================================
        // HELPER METHOD (Add this to the bottom of the LeadsController)
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
        // 🚀 NEW: BULK EXPORT ENDPOINTS (WITH CHUNKING FOR >100 RECORDS)
        // ==========================================
        [HttpGet]
        [Route("Leads/Export")]
        public async Task<IActionResult> Export(string format, string filters, string search = "")
        {
            try
            {
                var query = _context.Leads.AsNoTracking().AsQueryable();

                // 1. Role / User visibility restrictions
                string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var currentUser = await _context.Users.FindAsync(currentUserId);
                bool isAdmin = !string.IsNullOrWhiteSpace(currentUser?.Profile) &&
                               (currentUser.Profile.Contains("Admin", StringComparison.OrdinalIgnoreCase) ||
                                currentUser.Profile.Equals("Administrator", StringComparison.OrdinalIgnoreCase));

                if (!isAdmin && currentUser != null)
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
                        .Select(u => u.Id).ToListAsync();

                    visibleUserIds.Add(currentUserId);
                    query = query.Where(a => visibleUserIds.Contains(a.LeadOwnerId));
                }

                // 2. Search Box Filter
                if (!string.IsNullOrEmpty(search))
                {
                    var searchLower = search.ToLower();
                    query = query.Where(a =>
                        (a.LeadName != null && a.LeadName.ToLower().Contains(searchLower)) ||
                        (a.MobileNumber != null && a.MobileNumber.Contains(search)) ||
                        (a.EmailID != null && a.EmailID.ToLower().Contains(searchLower))
                    );
                }

                // 3. Dynamic Filter Expressions (Same logic as Index)
                if (!string.IsNullOrWhiteSpace(filters) && filters != "[]")
                {
                    try
                    {
                        string jsonString = filters.Contains("%5B") ? Uri.UnescapeDataString(filters) : filters;
                        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var filterList = System.Text.Json.JsonSerializer.Deserialize<List<LeadFilterCriteria>>(jsonString, options);

                        if (filterList != null && filterList.Any())
                        {
                            var parameter = System.Linq.Expressions.Expression.Parameter(typeof(Lead), "a");
                            System.Linq.Expressions.Expression combinedPredicate = null;

                            foreach (var f in filterList.Where(f => !f.ColumnName.StartsWith("Task.") && f.ColumnName != "RelatedTasksPresence"))
                            {
                                if (string.IsNullOrWhiteSpace(f.Value) && f.Condition != "is_empty" && f.Condition != "is_not_empty") continue;

                                string dbColName = f.ColumnName;
                                var propertyInfo = typeof(Lead).GetProperty(dbColName, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                                // Safe resolution for ID foreign keys
                                if (propertyInfo != null && propertyInfo.PropertyType.IsClass && propertyInfo.PropertyType != typeof(string))
                                {
                                    var idProp = typeof(Lead).GetProperty(dbColName + "Id", System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                    if (idProp != null) { propertyInfo = idProp; }
                                }

                                if (propertyInfo == null) continue;

                                var propExpr = System.Linq.Expressions.Expression.Property(parameter, propertyInfo);
                                System.Linq.Expressions.Expression conditionExpr = null;
                                string safeValue = f.Value?.Replace("+", " ").Trim() ?? "";

                                if (propertyInfo.PropertyType == typeof(string))
                                {
                                    var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes);
                                    var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) });
                                    var notNullProp = System.Linq.Expressions.Expression.Coalesce(propExpr, System.Linq.Expressions.Expression.Constant(""));
                                    var lowerProp = System.Linq.Expressions.Expression.Call(notNullProp, toLowerMethod);
                                    var lowerVal = System.Linq.Expressions.Expression.Constant(safeValue.ToLower());

                                    if (f.Condition == "is") conditionExpr = System.Linq.Expressions.Expression.Equal(lowerProp, lowerVal);
                                    else if (f.Condition == "is_not") conditionExpr = System.Linq.Expressions.Expression.NotEqual(lowerProp, lowerVal);
                                    else if (f.Condition == "contains") conditionExpr = System.Linq.Expressions.Expression.Call(lowerProp, containsMethod, lowerVal);
                                    else if (f.Condition == "does_not_contain") conditionExpr = System.Linq.Expressions.Expression.Not(System.Linq.Expressions.Expression.Call(lowerProp, containsMethod, lowerVal));
                                    else if (f.Condition == "is_empty") conditionExpr = System.Linq.Expressions.Expression.Call(typeof(string).GetMethod("IsNullOrEmpty"), propExpr);
                                    else if (f.Condition == "is_not_empty") conditionExpr = System.Linq.Expressions.Expression.Not(System.Linq.Expressions.Expression.Call(typeof(string).GetMethod("IsNullOrEmpty"), propExpr));
                                }
                                else if (propertyInfo.PropertyType == typeof(bool) || propertyInfo.PropertyType == typeof(bool?))
                                {
                                    if (bool.TryParse(safeValue, out bool boolVal))
                                    {
                                        var valExpr = System.Linq.Expressions.Expression.Constant(boolVal, propertyInfo.PropertyType);
                                        if (f.Condition == "is") conditionExpr = System.Linq.Expressions.Expression.Equal(propExpr, valExpr);
                                        else if (f.Condition == "is_not") conditionExpr = System.Linq.Expressions.Expression.NotEqual(propExpr, valExpr);
                                    }
                                }

                                if (conditionExpr != null)
                                {
                                    combinedPredicate = combinedPredicate == null ? conditionExpr :
                                        (f.LogicalOperator.Equals("OR", StringComparison.OrdinalIgnoreCase) ?
                                         System.Linq.Expressions.Expression.OrElse(combinedPredicate, conditionExpr) :
                                         System.Linq.Expressions.Expression.AndAlso(combinedPredicate, conditionExpr));
                                }
                            }

                            if (combinedPredicate != null)
                            {
                                var lambda = System.Linq.Expressions.Expression.Lambda<Func<Lead, bool>>(combinedPredicate, parameter);
                                query = query.Where(lambda);
                            }
                        }
                    }
                    catch { /* Ignore parse errors */ }
                }

                // 🚀 FIX: Prevent Nullable IQueryable crashes by excluding virtual lists and only allowing primitive/nullable types
                var excludedFields = new[] { "ZohoRecordId", "Notes", "Tasks", "Deals", "PaymentRow", "LeadOwner", "AccountOwner", "CoOwner", "DemoOwner", "Note" };
                var properties = typeof(Lead).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                    .Where(p => !excludedFields.Contains(p.Name) &&
                                (!p.PropertyType.IsGenericType || p.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>)))
                    .ToArray();

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                if (format.Equals("xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    using var workbook = new ClosedXML.Excel.XLWorkbook();
                    var worksheet = workbook.Worksheets.Add("Leads");

                    for (int i = 0; i < properties.Length; i++)
                    {
                        worksheet.Cell(1, i + 1).Value = properties[i].Name;
                        worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                    }

                    int rowIdx = 2;
                    await foreach (var lead in query.AsAsyncEnumerable())
                    {
                        for (int i = 0; i < properties.Length; i++)
                        {
                            worksheet.Cell(rowIdx, i + 1).Value = properties[i].GetValue(lead, null)?.ToString() ?? "";
                        }
                        rowIdx++;
                    }

                    worksheet.Columns().AdjustToContents();
                    var memoryStream = new System.IO.MemoryStream();
                    workbook.SaveAs(memoryStream);
                    memoryStream.Position = 0;

                    return File(memoryStream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Leads_All_{timestamp}.xlsx");
                }
                else
                {
                    Response.Clear();
                    Response.ContentType = "text/csv";
                    Response.Headers.Add("Content-Disposition", $"attachment; filename=Leads_All_{timestamp}.csv");

                    await using var writer = new System.IO.StreamWriter(Response.Body, System.Text.Encoding.UTF8, bufferSize: 1024, leaveOpen: true);
                    await writer.WriteLineAsync(string.Join(",", properties.Select(p => $"\"{p.Name}\"")));

                    await foreach (var lead in query.AsAsyncEnumerable())
                    {
                        var rowValues = properties.Select(p =>
                        {
                            var val = p.GetValue(lead, null)?.ToString() ?? "";
                            return $"\"{val.Replace("\"", "\"\"")}\"";
                        });
                        await writer.WriteLineAsync(string.Join(",", rowValues));
                    }

                    await writer.FlushAsync();
                    return new EmptyResult();
                }
            }
            catch (Exception ex)
            {
                return BadRequest($"Export failed: {ex.Message}");
            }
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Route("Leads/ExportSelected")]
        public async Task<IActionResult> ExportSelected([FromQuery] string format, [FromForm] string selectedIds)
        {
            try
            {
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
                };
                var ids = System.Text.Json.JsonSerializer.Deserialize<List<int>>(selectedIds, options) ?? new List<int>();

                if (!ids.Any()) return BadRequest("No records selected.");

                // 🚀 FIX: Prevent Nullable IQueryable crashes by excluding virtual lists and only allowing primitive/nullable types
                var excludedFields = new[] { "ZohoRecordId", "Notes", "Tasks", "Deals", "PaymentRow", "LeadOwner", "AccountOwner", "CoOwner", "DemoOwner", "Note" };
                var properties = typeof(Lead).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                    .Where(p => !excludedFields.Contains(p.Name) &&
                                (!p.PropertyType.IsGenericType || p.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>)))
                    .ToArray();

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                if (format.Equals("xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    using var workbook = new ClosedXML.Excel.XLWorkbook();
                    var worksheet = workbook.Worksheets.Add("Selected Leads");

                    for (int i = 0; i < properties.Length; i++)
                    {
                        worksheet.Cell(1, i + 1).Value = properties[i].Name;
                        worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                    }

                    int rowIdx = 2;
                    int chunkSize = 2000; // 🚀 CHUNKING RESOLVES THE > 2100 SQL PARAMETER CRASH

                    for (int i = 0; i < ids.Count; i += chunkSize)
                    {
                        var batch = ids.Skip(i).Take(chunkSize).ToList();
                        var chunkData = await _context.Leads.AsNoTracking().Where(l => batch.Contains(l.Id)).ToListAsync();

                        foreach (var lead in chunkData)
                        {
                            for (int col = 0; col < properties.Length; col++)
                            {
                                worksheet.Cell(rowIdx, col + 1).Value = properties[col].GetValue(lead, null)?.ToString() ?? "";
                            }
                            rowIdx++;
                        }
                    }

                    var ms = new System.IO.MemoryStream();
                    workbook.SaveAs(ms);
                    return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Leads_Selected_{timestamp}.xlsx");
                }
                else
                {
                    Response.Clear();
                    Response.ContentType = "text/csv";
                    Response.Headers.Add("Content-Disposition", $"attachment; filename=Leads_Selected_{timestamp}.csv");

                    await using var writer = new System.IO.StreamWriter(Response.Body, System.Text.Encoding.UTF8, bufferSize: 1024, leaveOpen: true);
                    await writer.WriteLineAsync(string.Join(",", properties.Select(p => $"\"{p.Name}\"")));

                    int chunkSize = 2000; // 🚀 CHUNKING RESOLVES THE > 2100 SQL PARAMETER CRASH
                    for (int i = 0; i < ids.Count; i += chunkSize)
                    {
                        var batch = ids.Skip(i).Take(chunkSize).ToList();
                        var chunkData = await _context.Leads.AsNoTracking().Where(l => batch.Contains(l.Id)).ToListAsync();

                        foreach (var lead in chunkData)
                        {
                            var rowValues = properties.Select(p =>
                            {
                                var val = p.GetValue(lead, null)?.ToString() ?? "";
                                return $"\"{val.Replace("\"", "\"\"")}\"";
                            });
                            await writer.WriteLineAsync(string.Join(",", rowValues));
                        }
                    }

                    await writer.FlushAsync();
                    return new EmptyResult();
                }
            }
            catch (Exception ex)
            {
                return BadRequest($"Export failed: {ex.Message}");
            }
        }

        // GET: Leads/Create
        public IActionResult Create()
        {
            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            ViewBag.AccountsList = new SelectList(_context.Accounts, "Id", "AccountName");
            ViewBag.VendorsList = new SelectList(_context.Vendors, "Id", "VendorName");

            // 🚀 Binds to ProductId and passes dictionary for Javascript pricing
            ViewBag.ProductsList = new SelectList(_context.Products, "Id", "ProductName");
            ViewBag.ProductPrices = _context.Products.ToDictionary(p => p.Id, p => p.UnitPrice);

            return View(new Lead());
        }

        // ==========================================
        // POST: Leads/Create
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Lead lead)
        {
            var paymentKeys = ModelState.Keys.Where(k => k.StartsWith("PaymentRow")).ToList();
            foreach (var key in paymentKeys)
            {
                ModelState.Remove(key);
            }
            ModelState.Remove("PaymentRow");

            string currentUser = User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(User.Identity.Name) ? User.Identity.Name : "System User";
            lead.CreatedBy = $"{currentUser} on {DateTime.Now.ToString("MMM dd, yyyy - hh:mm tt")}";
            lead.CreatedDateAndTime = DateTime.Now;
            lead.ModifiedTime = DateTime.Now;
            ModelState.Remove("CreatedBy");

            if (ModelState.IsValid)
            {
                // =========================================================
                // AUTOMATED PIPELINE ASSIGNMENT
                // =========================================================
                if (lead.CurrentStatus == "User")
                {
                    lead.Pipeline = "Upgrade Software";
                }
                else if (lead.CurrentStatus == "Non User")
                {
                    lead.Pipeline = "New Software";
                }

                // 🚀 FIX: Prevent EF from Double-Inserting Products
                var rowsToSave = new List<ProductPaymentRow>();
                if (lead.PaymentRow != null)
                {
                    rowsToSave = lead.PaymentRow
                        .Where(r => (r.ProductId.HasValue && r.ProductId.Value > 0) || !string.IsNullOrWhiteSpace(r.ProductName))
                        .ToList();
                }
                // Detach the list so Entity Framework doesn't save them automatically (we will do it manually to prevent duplicates)
                lead.PaymentRow = null;

                // 1. Save new lead to DB
                _context.Add(lead);
                await _context.SaveChangesAsync(); // Generates the new Lead.Id

                // 2. Manually link and save the products
                if (rowsToSave.Any())
                {
                    foreach (var row in rowsToSave)
                    {
                        row.Id = 0; // Force it to be a new record
                        row.LeadId = lead.Id;
                        _context.Add(row);
                    }
                    await _context.SaveChangesAsync();
                }

                return RedirectToAction("Edit", new { id = lead.Id });
            }

            // Fallback if model state fails
            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            ViewBag.AccountsList = new SelectList(_context.Accounts, "Id", "AccountName");
            ViewBag.ProductsList = new SelectList(_context.Products, "Id", "ProductName");
            ViewBag.VendorsList = new SelectList(_context.Vendors, "Id", "VendorName");
            ViewBag.ProductPrices = _context.Products.ToDictionary(p => p.Id, p => p.UnitPrice);

            return View(lead);
        }

        [HttpGet]
        public async Task<IActionResult> GetNotes(int leadId)
        {
            // 1. Fetch the AccountId linked to this Lead
            var accountId = await _context.Leads
                .Where(l => l.Id == leadId)
                .Select(l => l.AccountId)
                .FirstOrDefaultAsync();

            // 2. Fetch notes belonging to the Lead OR its linked Account
            var notes = await _context.Note
                .Where(n => n.LeadId == leadId || (accountId != null && n.AccountId == accountId))
                .OrderByDescending(n => n.CreatedDateTime)
                .Select(n => new {
                    ownerName = n.NoteOwner != null ? n.NoteOwner.fullName : "System",
                    createdDateTime = n.CreatedDateTime.ToString("MMM dd, yyyy h:mm tt"),
                    noteTitle = n.NoteTitle,
                    description = n.Description,
                    attachmentFileName = n.AttachmentFileName,

                    // Optional: Adds a flag so your frontend knows where the note came from
                    source = n.LeadId == leadId ? "Lead Note" : "Account Note"
                }).ToListAsync();

            return Json(notes);
        }

        [HttpPost]
        public async Task<IActionResult> SaveNoteAjax(int leadId, string noteTitle, string description, string ownerId, IFormFile attachment)
        {
            try
            {
                var note = new Notes
                {
                    LeadId = leadId,
                    NoteOwnerId = string.IsNullOrWhiteSpace(ownerId) ? null : ownerId,
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

                string ownerName = "System User";
                if (!string.IsNullOrEmpty(note.NoteOwnerId))
                {
                    var user = await _context.Users.FindAsync(note.NoteOwnerId);
                    if (user != null) ownerName = user.fullName ?? user.FirstName;
                }

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
        // 🚀 NEW: BULK CREATE TASKS FOR SELECTED LEADS
        // ==========================================
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> BulkCreateTasks([FromBody] BulkTaskCreateRequest request)
        {
            if (request == null || request.LeadIds == null || !request.LeadIds.Any() || string.IsNullOrWhiteSpace(request.Subject))
            {
                return Json(new { success = false, message = "Please provide a Subject and select at least one Lead." });
            }

            try
            {
                string currentUserName = User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(User.Identity.Name)
                    ? User.Identity.Name
                    : "System User";

                string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // Fetch selected leads to automatically link their AccountId or Mobile if not manually overridden
                var selectedLeads = await _context.Leads
                    .Where(l => request.LeadIds.Contains(l.Id))
                    .AsNoTracking()
                    .ToListAsync();

                var tasksToInsert = new List<CrmTaskModel>();

                foreach (var lead in selectedLeads)
                {
                    var newTask = new CrmTaskModel
                    {
                        Subject = request.Subject.Trim(),
                        LeadId = lead.Id,
                        AccountId = request.AccountId ?? lead.AccountId,
                        DueDate = request.DueDate,
                        Priority = string.IsNullOrWhiteSpace(request.Priority) ? "High" : request.Priority,
                        TaskOwnerId = !string.IsNullOrWhiteSpace(request.TaskOwnerId) ? request.TaskOwnerId : (lead.LeadOwnerId ?? currentUserId),
                        Status = string.IsNullOrWhiteSpace(request.Status) ? "Not Started" : request.Status,
                        CreatedBy = $"{currentUserName} on {DateTime.Now:dd/MM/yy HH:mm}",
                        CreatedTime = DateTime.Now
                    };

                    tasksToInsert.Add(newTask);
                }

                if (tasksToInsert.Any())
                {
                    await _context.Tasks.AddRangeAsync(tasksToInsert);
                    await _context.SaveChangesAsync();
                }

                return Json(new { success = true, count = tasksToInsert.Count });
            }
            catch (Exception ex)
            {
                string message = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = message });
            }
        }

        public class BulkTaskCreateRequest
        {
            public List<int> LeadIds { get; set; }
            public string Subject { get; set; }
            public int? AccountId { get; set; }
            public DateTime? DueDate { get; set; }
            public string Priority { get; set; }
            public string TaskOwnerId { get; set; }
            public string Mobile { get; set; }
            public string LeadStatus { get; set; }
            public string Status { get; set; }
        }

        // ==========================================
        // EDIT NOTE VIA AJAX (LEADS)
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> EditNoteAjax(int noteId, string noteTitle, string description)
        {
            try
            {
                var note = await _context.Note.FindAsync(noteId);
                if (note == null) return Json(new { success = false, message = "Note not found in the database." });

                note.NoteTitle = noteTitle ?? "";
                note.Description = description ?? "";
                note.NoteContent = description ?? "";

                _context.Note.Update(note);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult DownloadNoteAttachment(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return BadRequest("Filename missing.");

            string folderPath = @"C:\CRM_Files\Notes";
            string filePath = Path.Combine(folderPath, fileName);

            if (!System.IO.File.Exists(filePath))
            {
                var matchingFiles = Directory.GetFiles(folderPath, "*_" + fileName);
                if (matchingFiles.Length > 0)
                {
                    filePath = matchingFiles[0];
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
        // BULK & SINGLE DELETE: AJAX POST
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> BulkDelete([FromBody] List<int> ids)
        {
            if (ids == null || !ids.Any())
                return Json(new { success = false, message = "No records selected." });

            if (_context == null || _context.Leads == null)
                return Json(new { success = false, message = "Database context is not initialized." });

            try
            {
                var leadsToDelete = await _context.Leads
                                                  .Where(l => ids.Contains(l.Id))
                                                  .ToListAsync();

                if (leadsToDelete != null && leadsToDelete.Any())
                {
                    if (_context.Note != null)
                    {
                        var relatedNotes = await _context.Note
                            .Where(n => n.LeadId != null && ids.Contains(n.LeadId.Value))
                            .ToListAsync();

                        if (relatedNotes != null && relatedNotes.Any())
                        {
                            _context.Note.RemoveRange(relatedNotes);
                        }
                    }

                    _context.Leads.RemoveRange(leadsToDelete);
                    await _context.SaveChangesAsync();
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                string errorMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "Error: " + errorMsg });
            }
        }

        // GET: Leads/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var lead = await _context.Leads
                .Include(l => l.PaymentRow)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (lead == null) return NotFound();

            // 🚀 FIX: If the lead was saved without a stage, default it to Open in the Edit view
            if (string.IsNullOrEmpty(lead.Stage))
            {
                lead.Stage = "Open";
            }

            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            ViewBag.AccountsList = new SelectList(_context.Accounts, "Id", "AccountName");
            ViewBag.ProductsList = new SelectList(_context.Products, "Id", "ProductName");
            ViewBag.VendorsList = new SelectList(_context.Vendors, "Id", "VendorName");
            ViewBag.ProductPrices = _context.Products.ToDictionary(p => p.Id, p => p.UnitPrice);

            // 🚀 NEW: Fetch the Deals linked to this Lead
            ViewBag.RelatedDeals = await _context.Deals
                .Where(d => d.LeadId == id)
                .ToListAsync();

            // 🚀 NEW: Fetch the Account linked to this Lead (if applicable)
            if (lead.AccountId != null && lead.AccountId != 0)
            {
                ViewBag.RelatedAccounts = await _context.Accounts
                    .Where(a => a.Id == lead.AccountId)
                    .ToListAsync();
            }

            // 🚀 FIX: Fetch notes for both this Lead AND its associated Account
            ViewBag.ExistingNotes = await _context.Note
                .Include(n => n.NoteOwner)
                .Where(n => n.LeadId == id || (lead.AccountId != null && n.AccountId == lead.AccountId))
                .OrderByDescending(n => n.CreatedDateTime)
                .ToListAsync();

            // 🚀 ADDED: Fetch all tasks belonging to this Lead
            ViewBag.Tasks = await _context.Tasks
                .Where(t => t.LeadId == id)
                .ToListAsync();

            // =========================================================
            // POPULATE THE SIDEBAR ACCOUNT & DEAL
            // =========================================================

            // 1. Get the Account Name
            if (lead.AccountId != null && lead.AccountId > 0)
            {
                var account = await _context.Accounts.FindAsync(lead.AccountId);
                lead.AccountName = account?.AccountName;
            }

            // 2. Get the linked Deal (Grabbing the most recent deal created for this lead)
            if (!string.IsNullOrEmpty(lead.LeadName))
            {
                var linkedDeal = await _context.Deals
                    .Where(d => d.LeadName == lead.LeadName)
                    .OrderByDescending(d => d.Id)
                    .FirstOrDefaultAsync();

                if (linkedDeal != null)
                {
                    lead.DealId = linkedDeal.Id;
                    lead.DealName = linkedDeal.DealName;
                }
            }

            // 🚀 FIX: explicitly fetch the Account using the Lead's AccountId column
            if (lead.AccountId != null && lead.AccountId != 0)
            {
                ViewBag.RelatedAccounts = await _context.Accounts
                    .Where(a => a.Id == lead.AccountId)
                    .ToListAsync();
            }
            else
            {
                ViewBag.RelatedAccounts = new List<Radar_CRM.Models.Account>();
            }

            return View(lead);
        }

        // POST: Leads/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Lead lead)
        {
            if (id != lead.Id)
            {
                return NotFound();
            }

            ModelState.Remove("PaymentRow");
            ModelState.Remove("LeadOwner");
            ModelState.Remove("AccountOwner");
            ModelState.Remove("DemoOwner");
            ModelState.Remove("CoOwner");
            var paymentKeys = ModelState.Keys.Where(k => k.StartsWith("PaymentRow")).ToList();
            foreach (var key in paymentKeys)
            {
                ModelState.Remove(key);
            }

            if (ModelState.IsValid)
            {
                // 🚀 1. SET "MODIFIED BY" AND TIME
                string currentUser = User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(User.Identity.Name) ? User.Identity.Name : "System User";
                lead.ModifiedBy = currentUser;
                lead.ModifiedTime = DateTime.Now; // 🚀 ADD THIS LINE TO SET ACTUAL MODIFIED DATE

                ModelState.Remove("ModifiedBy");
                ModelState.Remove("ModifiedTime"); // Add this to prevent validation errors on the new date

                // 🚀 2. FETCH ORIGINAL RECORD TO PREVENT "CREATED BY" FROM BEING ERASED
                var existingLead = await _context.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lead.Id);
                // 🚀 FIX: Grab the User's ID to save in the Timeline Log
                string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "System";

                // =========================================================
                // 🚀 ZOHO-STYLE TIMELINE TRACKER ENGINE (LEADS)
                // =========================================================
                if (existingLead != null)
                {
                    var timelineEvents = new List<TimelineEvent>();
                    var excludedAuditFields = new[] { "ModifiedBy", "ModifiedTime", "CreatedBy", "LeadCreatedTime", "CreatedDateAndTime" };

                    // Dynamically compare every single property on the Lead
                    var properties = typeof(Lead).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                         .Where(p => !excludedAuditFields.Contains(p.Name) && (!p.PropertyType.IsGenericType || p.PropertyType.GetGenericTypeDefinition() != typeof(List<>)));

                    foreach (var prop in properties)
                    {
                        var oldObj = prop.GetValue(existingLead);
                        var newObj = prop.GetValue(lead);

                        // 🚀 BULLETPROOF FIX: Treat nulls, empty strings, and white spaces as the exact same thing
                        string oldStr = oldObj?.ToString()?.Trim() ?? "";
                        string newStr = newObj?.ToString()?.Trim() ?? "";

                        string oldVal = string.IsNullOrEmpty(oldStr) ? "blank value" : oldStr;
                        string newVal = string.IsNullOrEmpty(newStr) ? "blank value" : newStr;

                        // If the field TRULY changed, log it!
                        if (oldVal != newVal)
                        {
                            string displayFieldName = prop.Name;

                            // 🚀 RESOLVE OWNER ID TO ACTUAL NAME FOR TIMELINE
                            if (prop.Name.EndsWith("OwnerId", StringComparison.OrdinalIgnoreCase))
                            {
                                displayFieldName = prop.Name.Replace("Id", ""); // e.g., "LeadOwnerId" -> "LeadOwner"

                                if (oldVal != "blank value")
                                {
                                    var oldUser = await _context.Users.FindAsync(oldStr);
                                    if (oldUser != null) oldVal = oldUser.fullName ?? oldUser.FirstName ?? oldVal;
                                }
                                if (newVal != "blank value")
                                {
                                    var newUser = await _context.Users.FindAsync(newStr);
                                    if (newUser != null) newVal = newUser.fullName ?? newUser.FirstName ?? newVal;
                                }
                            }

                            timelineEvents.Add(new TimelineEvent
                            {
                                EntityType = "Lead",
                                EntityId = lead.Id,
                                EventType = "FieldUpdate",
                                FieldName = prop.Name,
                                OldValue = oldVal,
                                NewValue = newVal,
                                ActionDescription = $"{displayFieldName} was updated from <b>{oldVal}</b> to <b>{newVal}</b>",
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

                if (existingLead != null && string.IsNullOrEmpty(lead.CreatedBy))
                {
                    lead.CreatedBy = existingLead.CreatedBy;
                }
                if (lead.CurrentStatus == "User")
                {
                    lead.Pipeline = "Upgrade Software";
                }
                else if (lead.CurrentStatus == "Non User")
                {
                    lead.Pipeline = "New Software";
                }

                try
                {
                    // =========================================================
                    // 🚀 FIX: SAVE NEW NOTES APPENDED BY AJAX
                    // =========================================================
                    var newNoteOwners = Request.Form["SavedNoteOwner[]"];
                    var newNoteDates = Request.Form["SavedNoteDateTime[]"];
                    var newNoteDescs = Request.Form["SavedNoteDesc[]"];

                    if (newNoteDescs.Count > 0)
                    {
                        for (int i = 0; i < newNoteDescs.Count; i++)
                        {
                            var noteDesc = newNoteDescs[i];
                            if (!string.IsNullOrWhiteSpace(noteDesc))
                            {
                                string rawOwnerId = newNoteOwners.Count > i ? newNoteOwners[i] : null;
                                string safeOwnerId = string.IsNullOrWhiteSpace(rawOwnerId) ? null : rawOwnerId;

                                var newNote = new Notes
                                {
                                    LeadId = lead.Id,
                                    NoteOwnerId = safeOwnerId,
                                    Description = noteDesc,
                                    CreatedDateTime = DateTime.TryParse(newNoteDates.Count > i ? newNoteDates[i] : "", out DateTime dt) ? dt : DateTime.Now
                                };
                                _context.Note.Add(newNote);
                            }
                        }
                        // Save the notes so they are immediately available for the Deal Transfer query
                        await _context.SaveChangesAsync();
                    }

                   
                    // =========================================================
                    // HANDOVER LEAD AUTOMATION
                    // =========================================================
                    bool isHandoverConditionMet = (lead.Stage == "Demo Done" && lead.LeadStatus == "Hot") ||
                                                   (lead.LeadStatus == "Warm");

                    if (isHandoverConditionMet)
                    {
                        var exemptUsers = await _context.Users
                            .Where(u => u.FirstName.Contains("Jaspal") ||
                                        u.FirstName.Contains("Akash") ||
                                        u.FirstName.Contains("Abhinav"))
                            .ToListAsync();

                        var exemptOwnerIds = exemptUsers.Select(u => u.Id).ToList();
                        string currentOwnerId = lead.LeadOwnerId;

                        if (string.IsNullOrWhiteSpace(lead.DemoOwnerId))
                        {
                            lead.DemoOwnerId = currentOwnerId;
                        }

                        if (currentOwnerId != null && !exemptOwnerIds.Contains(currentOwnerId))
                        {
                            var defaultNewOwner = exemptUsers.FirstOrDefault(u => u.FirstName.Contains("Jaspal"));
                            if (defaultNewOwner != null)
                            {
                                lead.LeadOwnerId = defaultNewOwner.Id;
                            }
                        }
                    }

                    // =========================================================
                    // 🚀 FIX: PAYMENT ROWS DUPLICATION SYNCHRONIZATION
                    // =========================================================
                    if (lead.PaymentRow != null)
                    {
                        var validRows = lead.PaymentRow
                            .Where(r => (r.ProductId.HasValue && r.ProductId.Value > 0) || !string.IsNullOrWhiteSpace(r.ProductName))
                            .ToList();

                        lead.PaymentRow = null;

                        var oldRows = _context.Set<ProductPaymentRow>().Where(r => r.LeadId == lead.Id).ToList();
                        if (oldRows.Any())
                        {
                            _context.RemoveRange(oldRows);
                        }

                        foreach (var row in validRows)
                        {
                            row.Id = 0;
                            row.LeadId = lead.Id;
                            _context.Add(row);
                        }
                    }

                    // Update existing lead in the DB
                    _context.Update(lead);
                    await _context.SaveChangesAsync();

                    // =========================================================
                    // 🚀 NEW: TRANSFER TASKS IF LEAD OWNER CHANGED
                    // =========================================================
                    bool ownerChanged = existingLead != null &&
                         !string.Equals(existingLead.LeadOwnerId?.Trim(), lead.LeadOwnerId?.Trim(), StringComparison.OrdinalIgnoreCase);

                    if (ownerChanged && !string.IsNullOrWhiteSpace(lead.LeadOwnerId))
                    {
                        // 1. FETCH THE NEW OWNER'S NAME (Crucial for the UI)
                        string newOwnerName = "System User";
                        var newOwnerUser = await _context.Users.FindAsync(lead.LeadOwnerId);
                        if (newOwnerUser != null)
                        {
                            newOwnerName = newOwnerUser.fullName ?? newOwnerUser.FirstName ?? "System User";
                        }

                        // 2. Fetch all incomplete tasks related to this Lead
                        var relatedTasks = await _context.Set<Radar_CRM.Models.Task>()
                            .Where(t => t.LeadId == lead.Id && t.Status != "Completed")
                            .ToListAsync();

                        if (relatedTasks.Any())
                        {
                            foreach (var task in relatedTasks)
                            {
                                // 3. UPDATE BOTH THE ID *AND* THE TEXT NAME
                                task.TaskOwnerId = lead.LeadOwnerId;
                                task.TaskOwner = newOwnerName;

                                task.ModifiedBy = $"{currentUser} (System Transfer) on {DateTime.Now.ToString("MMM dd, yyyy - hh:mm tt")}";
                                task.ModifiedTime = DateTime.Now;

                                // 4. Force Entity Framework to track this specific modification
                                _context.Entry(task).State = EntityState.Modified;
                            }
                            _context.Set<Radar_CRM.Models.Task>().UpdateRange(relatedTasks);
                            await _context.SaveChangesAsync();
                        }

                        // 🚀 FIX: Renamed to 'notifiedLeadDict' to prevent the scope error
                        var notifiedLeadDict = new Dictionary<int, string> { { lead.Id, lead.LeadName ?? "Unknown Lead" } };
                        await NotifyNewOwnerAsync(lead.LeadOwnerId, notifiedLeadDict);
                    }

                  

                    // =========================================================
                    // 🚀 NEW: SYNC UPDATES TO THE LINKED ACCOUNT
                    // =========================================================
                    // 🚀 FIX: Declare trueAccountName OUTSIDE the if-block so the Deal logic can see it
                    string trueAccountName = lead.AccountName;

                    if (lead.AccountId.HasValue)
                    {
                        var linkedAccount = await _context.Accounts.FindAsync(lead.AccountId.Value);
                        if (linkedAccount != null)
                        {
                            // Capture the true account name so we can pass it to the Deal later
                            trueAccountName = linkedAccount.AccountName;

                            // --- Core Identifiers ---
                            linkedAccount.ContactPersonName = lead.ContactName;

                            // --- Ownership Mapping ---
                            linkedAccount.AccountOwnerId = lead.AccountOwnerId;
                            linkedAccount.CoOwnerId = lead.CoOwnerId;

                            // --- Source Mapping ---
                            linkedAccount.DataSource = lead.DataSources;
                            linkedAccount.MetaCampaignName = lead.MetaCampaignName;
                            linkedAccount.SeminarName = lead.SeminarName;

                            // --- Basic Details ---
                            linkedAccount.MobileNumber = lead.MobileNumber;
                            linkedAccount.AlternateMobile = lead.AlternateMobile;
                            linkedAccount.Email = lead.EmailID;
                            linkedAccount.AlternateEmailID = lead.AlternateEmailID;
                            linkedAccount.AccountType = lead.AccountType;
                            linkedAccount.CurrentStatus = lead.CurrentStatus;

                            // --- Address Mapping ---
                            linkedAccount.Addr1_Country = lead.Addr1_Country;
                            linkedAccount.Addr1_FlatHouse = lead.Addr1_FlatHouse;
                            linkedAccount.Addr1_Street = lead.Addr1_Street;
                            linkedAccount.Addr1_City = lead.Addr1_City;
                            linkedAccount.Addr1_State = lead.Addr1_State;
                            linkedAccount.Addr1_Zip = lead.Addr1_Zip;

                            linkedAccount.Addr2_Country = lead.Addr2_Country;
                            linkedAccount.Addr2_FlatHouse = lead.Addr2_FlatHouse;
                            linkedAccount.Addr2_Street = lead.Addr2_Street;
                            linkedAccount.Addr2_City = lead.Addr2_City;
                            linkedAccount.Addr2_State = lead.Addr2_State;
                            linkedAccount.Addr2_Zip = lead.Addr2_Zip;

                            // --- Professional Mapping ---
                            linkedAccount.IsHomeopathicDoctor = lead.IsHomeopathicDoctor;
                            linkedAccount.ClinicType = lead.ClinicType;
                            linkedAccount.WorkType = lead.WorkType;
                            linkedAccount.HasComputer = lead.HasComputer;
                            linkedAccount.DateOfBirth = lead.DateOfBirth;
                            linkedAccount.Age = lead.Age;
                            linkedAccount.YearsOfPractice = lead.YearOfPractice;
                            linkedAccount.AveragePatientFee = lead.AveragePatientFee;
                            linkedAccount.PatientsPerDay = lead.PatientsPerDay;
                            linkedAccount.TotalExperience = lead.TotalExperience;
                            linkedAccount.NumberOfClinics = lead.NumberOfClinics;
                            linkedAccount.Qualification = lead.Qualification;
                            linkedAccount.YearOfPassing = lead.YearOfPassing;
                            linkedAccount.CollegeName = lead.CollegeName;

                            // --- Software & Purchases ---
                            linkedAccount.CurrentlyUsingSoftware = lead.CurrentlyUsingSoftware;
                            linkedAccount.CurrentSoftwareName = lead.CurrentSoftwareName;
                            linkedAccount.PurchaseDate = lead.PurchaseDate;
                            linkedAccount.PurchaseValue = lead.PurchaseValue;
                            linkedAccount.PaymentType = lead.PaymentType;
                            linkedAccount.PaymentStatus = lead.PaymentStatus;

                            linkedAccount.ContactPerson1 = lead.ContactPerson1;
                            linkedAccount.Contact1Phone = lead.Contact1Phone;
                            linkedAccount.ContactPerson2 = lead.ContactPerson2;
                            linkedAccount.Contact2Phone = lead.Contact2Phone;
                            linkedAccount.ContactPerson3 = lead.ContactPerson3;
                            linkedAccount.Contact3Phone = lead.Contact3Phone;

                            linkedAccount.Description = lead.Description;

                            _context.Update(linkedAccount);
                            await _context.SaveChangesAsync();
                        }
                    }

                    // =========================================================
                    // 🚀 DIRECT DEAL CREATION WITH NOTE & PAYMENT TRANSFER
                    // =========================================================
                    if (lead.Stage == "Won")
                    {
                        System.Diagnostics.Debug.WriteLine($"[DEBUG-NOTES] Checking if Deal exists for LeadName: '{lead.LeadName}'...");
                        bool dealExists = _context.Deals.Any(d => d.LeadName == lead.LeadName);

                        // 1. AUTOMATICALLY CHANGE LEAD STAGE TO 'WON'
                        lead.Stage = "Won";
                        _context.Update(lead);

                        if (!dealExists)
                        {
                            System.Diagnostics.Debug.WriteLine($"[DEBUG-NOTES] Deal does NOT exist. Proceeding to create Deal for Lead ID: {lead.Id}");
                            var savedRows = _context.Set<ProductPaymentRow>().Where(r => r.LeadId == lead.Id).ToList();

                            var newDeal = new Deal
                            {
                                LeadId = lead.Id,
                                DealName = lead.LeadName,
                                AccountId = lead.AccountId,

                                // 🚀 Passed the safely declared variable here
                                AccountName = trueAccountName,

                                ContactPersonName = lead.ContactName,
                                LeadName = lead.LeadName,
                                LeadSource = lead.DataSources,
                                DealOwnerId = lead.LeadOwnerId,
                                AccountOwner = lead.AccountOwnerId,
                                DemoOwner = lead.DemoOwnerId,
                                AccountType = lead.AccountType,
                                CreatedBy = lead.CreatedBy,
                                SeminarName = lead.SeminarName,
                                GroupName = lead.GroupName,

                                DateOfEntry = DateTime.Now,
                                MetaCampaignName = lead.MetaCampaignName,
                                PaymentMode = lead.PaymentMode,
                                PaymentType = lead.PaymentType,
                                PaymentStatus = lead.PaymentStatus,
                                SubTotal = lead.SubTotal ?? 0m,
                                Taxes = lead.Taxes ?? 0m,
                                Adjustment = lead.Adjustment ?? 0m,
                                GrandTotal = lead.GrandTotal ?? 0m
                            };
                            // 1. SAVE THE NEW DEAL FIRST TO GENERATE THE NEW DEAL ID
                            _context.Deals.Add(newDeal);
                            await _context.SaveChangesAsync();
                            System.Diagnostics.Debug.WriteLine($"[DEBUG-NOTES] Successfully created Deal with ID: {newDeal.Id}");

                            // 🚀 FIX 4: Explicitly force-save the Product & Payment rows using the newly generated Deal ID
                            if (savedRows.Any())
                            {
                                var dealPaymentRows = savedRows.Select(row => new DealPaymentRow
                                {
                                    DealId = newDeal.Id, // Link strictly to the new Deal
                                    DealType = row.DealType,
                                    ProductId = row.ProductId,
                                    ProductName = row.ProductName,
                                    Quantity = row.Quantity ?? 1,
                                    UnitPrice = row.ProductPrice ?? 0,
                                    Amount = row.Total ?? 0,
                                    Discount = row.DiscountPercent ?? 0,
                                    Total = row.FinalAmount ?? 0
                                }).ToList();

                                _context.AddRange(dealPaymentRows);
                                await _context.SaveChangesAsync();
                            }
                            System.Diagnostics.Debug.WriteLine($"[DEBUG-NOTES] Successfully created Deal with ID: {newDeal.Id}");

                            // ==========================================
                            // 2. 🚀 COPY/TRANSFER NOTES TO THE NEW DEAL
                            // ==========================================
                            System.Diagnostics.Debug.WriteLine($"[DEBUG-NOTES] Fetching notes for Lead ID: {lead.Id}...");
                            var leadNotes = await _context.Note
                                 .Where(n => n.LeadId == lead.Id)
                                 .AsNoTracking() // Ensures we create new copies, not modifying old ones
                                 .ToListAsync();

                            System.Diagnostics.Debug.WriteLine($"[DEBUG-NOTES] Found {leadNotes.Count} notes attached to Lead {lead.Id}");

                            if (leadNotes.Any())
                            {
                                var validUserIds = await _context.Users.Select(u => u.Id).ToListAsync();
                                var dealNotes = new List<Notes>();

                                foreach (var note in leadNotes)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[DEBUG-NOTES] Mapping Note ID {note.Id} | Original Owner: '{note.NoteOwnerId}'");

                                    // Validate the owner ID to prevent Foreign Key constraint crashes silently killing the save
                                    string safeOwnerId = null;
                                    if (!string.IsNullOrEmpty(note.NoteOwnerId) && validUserIds.Contains(note.NoteOwnerId))
                                    {
                                        safeOwnerId = note.NoteOwnerId;
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[DEBUG-NOTES] WARNING: NoteOwnerId '{note.NoteOwnerId}' is invalid or missing from Users table. Safely setting to null.");
                                    }

                                    dealNotes.Add(new Notes
                                    {
                                        DealId = newDeal.Id, // Link to the newly created Deal
                                        LeadId = null,       // Set to null so it uniquely maps to the Deal
                                        AccountId = null,

                                        NoteOwnerId = safeOwnerId,
                                        CreatedDateTime = note.CreatedDateTime,
                                        Description = note.Description,
                                        AttachmentFileName = note.AttachmentFileName,
                                        AttachmentFilePath = note.AttachmentFilePath
                                    });
                                }

                                try
                                {
                                    System.Diagnostics.Debug.WriteLine($"[DEBUG-NOTES] Attempting to save {dealNotes.Count} notes to DB for Deal {newDeal.Id}...");
                                    await _context.Note.AddRangeAsync(dealNotes);
                                    int rowsSaved = await _context.SaveChangesAsync();
                                    System.Diagnostics.Debug.WriteLine($"[DEBUG-NOTES] SUCCESS! {rowsSaved} note records saved successfully.");
                                }
                                catch (Exception ex)
                                {
                                    // THIS CATCHES SILENT DATABASE CRASHES
                                    System.Diagnostics.Debug.WriteLine($"[DEBUG-NOTES] ERROR SAVING NOTES: {ex.Message}");
                                    if (ex.InnerException != null)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[DEBUG-NOTES] INNER EXCEPTION: {ex.InnerException.Message}");
                                    }
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"[DEBUG-NOTES] Skipping transfer because 0 notes were found for Lead {lead.Id}.");
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[DEBUG-NOTES] Deal creation skipped because a Deal already exists for LeadName: '{lead.LeadName}'");
                        }
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!LeadExists(lead.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Edit), new { id = lead.Id });
            }

            // Fallback if model state fails
            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            ViewBag.AccountsList = new SelectList(_context.Accounts, "Id", "AccountName");
            ViewBag.ProductsList = new SelectList(_context.Products, "Id", "ProductName");
            ViewBag.ProductPrices = _context.Products.ToDictionary(p => p.Id, p => p.UnitPrice);

            return View(lead);
        }

        // ==========================================
        // QUICK INFO AUTO-SAVE: AJAX POST
        // ==========================================
        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Route("Leads/UpdateQuickInfo")]
        public async Task<IActionResult> UpdateQuickInfo(int id, string field, string newValue)
        {
            var lead = await _context.Leads.FindAsync(id);
            if (lead == null) return Json(new { success = false, message = "Lead not found." });

            string trimmedValue = newValue?.Trim() ?? "";

            // 🚀 CAPTURE OLD OWNER ID BEFORE UPDATING
            string oldOwnerId = lead.LeadOwnerId;

            try
            {
                var propertyInfo = typeof(Lead).GetProperty(field, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (propertyInfo != null)
                {
                    // 🚀 TIMELINE FIX: Capture old value before changing it
                    var oldObj = propertyInfo.GetValue(lead);
                    string oldStr = oldObj?.ToString()?.Trim() ?? "";
                    string oldVal = string.IsNullOrEmpty(oldStr) ? "blank value" : oldStr;

                    Type t = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;
                    object safeValue = null;

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
                    propertyInfo.SetValue(lead, safeValue, null);

                    // 🚀 TIMELINE FIX: Compare and Log for Quick Info
                    string newStr = safeValue?.ToString()?.Trim() ?? "";
                    string newVal = string.IsNullOrEmpty(newStr) ? "blank value" : newStr;

                    if (oldVal != newVal)
                    {
                        string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "System";
                        string currentUserNameLog = User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(User.Identity.Name) ? User.Identity.Name : "System User";

                        string displayFieldName = propertyInfo.Name;

                        // 🚀 RESOLVE OWNER ID TO ACTUAL NAME FOR TIMELINE
                        if (propertyInfo.Name.EndsWith("OwnerId", StringComparison.OrdinalIgnoreCase))
                        {
                            displayFieldName = propertyInfo.Name.Replace("Id", ""); // e.g., "LeadOwnerId" -> "LeadOwner"

                            if (oldVal != "blank value")
                            {
                                var oldUser = await _context.Users.FindAsync(oldStr);
                                if (oldUser != null) oldVal = oldUser.fullName ?? oldUser.FirstName ?? oldVal;
                            }
                            if (newVal != "blank value")
                            {
                                var newUser = await _context.Users.FindAsync(newStr);
                                if (newUser != null) newVal = newUser.fullName ?? newUser.FirstName ?? newVal;
                            }
                        }

                        var timelineEvent = new TimelineEvent
                        {
                            EntityType = "Lead",
                            EntityId = lead.Id,
                            EventType = "FieldUpdate",
                            FieldName = propertyInfo.Name,
                            OldValue = oldVal,
                            NewValue = newVal,
                            ActionDescription = $"{displayFieldName} was updated from <b>{oldVal}</b> to <b>{newVal}</b>",
                            CreatedById = currentUserId,
                            CreatedByName = currentUserNameLog,
                            CreatedDate = DateTime.Now
                        };
                        _context.TimelineEvents.Add(timelineEvent);
                    }

                    // =========================================================
                    // 🚀 HANDOVER LEAD AUTOMATION FOR QUICK EDIT
                    // =========================================================
                    if (field.Equals("LeadStatus", StringComparison.OrdinalIgnoreCase) || field.Equals("Stage", StringComparison.OrdinalIgnoreCase))
                    {
                        bool isHandoverConditionMet = (lead.Stage == "Demo Done" && lead.LeadStatus == "Hot") ||
                                                      (lead.LeadStatus == "Warm");

                        if (isHandoverConditionMet)
                        {
                            var exemptUsers = await _context.Users
                                .Where(u => u.FirstName.Contains("Jaspal") ||
                                            u.FirstName.Contains("Akash") ||
                                            u.FirstName.Contains("Abhinav"))
                                .ToListAsync();

                            var exemptOwnerIds = exemptUsers.Select(u => u.Id).ToList();
                            string currentOwnerId = lead.LeadOwnerId;

                            // 1. Save original owner to DemoOwner
                            if (string.IsNullOrWhiteSpace(lead.DemoOwnerId))
                            {
                                lead.DemoOwnerId = currentOwnerId;
                            }

                            // 2. Assign LeadOwner to Jaspal if not exempt
                            if (currentOwnerId != null && !exemptOwnerIds.Contains(currentOwnerId))
                            {
                                var defaultNewOwner = exemptUsers.FirstOrDefault(u => u.FirstName.Contains("Jaspal"));
                                if (defaultNewOwner != null)
                                {
                                    lead.LeadOwnerId = defaultNewOwner.Id;
                                }
                            }
                        }
                    }
                    // =========================================================
                }
                else
                {
                    return Json(new { success = false, message = "Invalid field mapping: " + field });
                }

                string currentUser = User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(User.Identity.Name) ? User.Identity.Name : "System User";
                lead.ModifiedBy = $"{currentUser} on {DateTime.Now.ToString("dd/MM/yy HH:mm")}";
                lead.ModifiedTime = DateTime.Now;

                _context.Update(lead);
                await _context.SaveChangesAsync();

                // =========================================================
                // 🚀 TRANSFER TASKS IF LEAD OWNER CHANGED (Manually OR via Handover Automation)
                // =========================================================
                bool ownerChanged = !string.Equals(oldOwnerId?.Trim(), lead.LeadOwnerId?.Trim(), StringComparison.OrdinalIgnoreCase);

                if (ownerChanged && !string.IsNullOrWhiteSpace(lead.LeadOwnerId))
                {
                    // 1. FETCH THE NEW OWNER'S NAME (Crucial for the UI)
                    string newOwnerName = "System User";
                    var newOwnerUser = await _context.Users.FindAsync(lead.LeadOwnerId);
                    if (newOwnerUser != null)
                    {
                        newOwnerName = newOwnerUser.fullName ?? newOwnerUser.FirstName ?? "System User";
                    }

                    var relatedTasks = await _context.Set<Radar_CRM.Models.Task>()
                        .Where(t => t.LeadId == lead.Id && t.Status != "Completed")
                        .ToListAsync();

                    if (relatedTasks.Any())
                    {
                        foreach (var task in relatedTasks)
                        {
                            // 2. UPDATE BOTH THE ID *AND* THE TEXT NAME
                            task.TaskOwnerId = lead.LeadOwnerId;
                            task.TaskOwner = newOwnerName;

                            task.ModifiedBy = $"{currentUser} (System Transfer) on {DateTime.Now.ToString("MMM dd, yyyy - hh:mm tt")}";
                            task.ModifiedTime = DateTime.Now;

                            // 3. Force Entity Framework to track this specific modification
                            _context.Entry(task).State = EntityState.Modified;
                        }
                        _context.Set<Radar_CRM.Models.Task>().UpdateRange(relatedTasks);
                        await _context.SaveChangesAsync();
                    }

                    // 🚀 NEW: SEND EMAIL NOTIFICATION TO THE NEW OWNER FOR SINGLE TRANSFER
                    var assignedLeadDict = new Dictionary<int, string> { { lead.Id, lead.LeadName ?? "Unknown Lead" } };
                    await NotifyNewOwnerAsync(lead.LeadOwnerId, assignedLeadDict);
                }
                // =========================================================

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                string errorDetails = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = errorDetails });
            }
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        [Route("Leads/UpdateTaskStatus")]
        public async Task<IActionResult> UpdateTaskStatus(int id, string status)
        {
            if (id <= 0 || string.IsNullOrWhiteSpace(status))
            {
                return Json(new { success = false, message = "Invalid Task ID or Status." }); // 🚀 FIX: Return JSON instead of BadRequest
            }

            try
            {
                var task = await _context.Tasks.FindAsync(id);
                if (task == null) return Json(new { success = false, message = "Task not found." }); // 🚀 FIX: Return JSON

                task.Status = status;

                string currentUser = User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(User.Identity.Name) ? User.Identity.Name : "System User";
                task.ModifiedBy = $"{currentUser} on {DateTime.Now.ToString("dd/MM/yy HH:mm")}";
                task.ModifiedTime = DateTime.Now;

                _context.Update(task);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Status updated successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Database Error: " + ex.Message });
            }
        }



        private bool LeadExists(int id)
        {
            return _context.Leads.Any(e => e.Id == id);
        }

        // ==========================================
        // BULK UPLOAD EXCEL/CSV (HIGH PERFORMANCE)
        // ==========================================
        [HttpPost]
        [DisableRequestSizeLimit]
        [RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = int.MaxValue)]
        public async Task<IActionResult> UploadFile(IFormFile uploadedFile)
        {
            if (uploadedFile == null || uploadedFile.Length == 0) return BadRequest("No file was uploaded.");

            var leadsToInsert = new List<Lead>();
            var leadsToUpdate = new List<Lead>();
            var skippedRecords = new List<string>();
            int currentRow = 1;

            // Load Maps for fast Upsert checking and Relationship mapping
            var validUserIds = new HashSet<string>(_context.Users.Select(u => u.Id), StringComparer.OrdinalIgnoreCase);

            var existingLeadsDb = await _context.Leads
                .Where(l => !string.IsNullOrEmpty(l.ZohoRecordId))
                .AsNoTracking()
                .GroupBy(l => l.ZohoRecordId)
                .ToDictionaryAsync(g => g.Key, g => g.First());

            var existingMobiles = new HashSet<string>(await _context.Leads.Where(l => !string.IsNullOrEmpty(l.MobileNumber)).Select(l => l.MobileNumber).ToListAsync());
            var existingEmails = new HashSet<string>(await _context.Leads.Where(l => !string.IsNullOrEmpty(l.EmailID)).Select(l => l.EmailID.ToLower()).ToListAsync());

            var accountLookup = _context.Accounts.Where(a => a.ZohoRecordId != null).ToDictionary(a => a.ZohoRecordId, a => a.Id);

            // Country Normalizer
            string NormalizeCountry(string input)
            {
                if (string.IsNullOrWhiteSpace(input)) return null;
                string upper = input.Trim().ToUpper();
                return (upper.Contains("INDIA") || upper == "IND" || upper == "IN") ? "INDIA" : upper;
            }

            try
            {
                using (var reader = new StreamReader(uploadedFile.OpenReadStream()))
                {
                    var headerLine = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(headerLine)) return BadRequest("Empty CSV");

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
                        string email = GetValSafe(values, "EmailID")?.ToLower();

                        bool isUpdate = false;
                        Lead lead;

                        // 🚀 UPSERT LOGIC
                        if (!string.IsNullOrEmpty(zohoRecordId) && existingLeadsDb.TryGetValue(zohoRecordId, out var existingLead))
                        {
                            lead = existingLead; // Found it -> Update
                            isUpdate = true;
                        }
                        else
                        {
                            // Duplication Check for purely new records
                            bool isMobileDup = !string.IsNullOrEmpty(mobile) && existingMobiles.Contains(mobile);
                            bool isEmailDup = !string.IsNullOrEmpty(email) && existingEmails.Contains(email);

                            if (isMobileDup || isEmailDup)
                            {
                                skippedRecords.Add($"Row {currentRow}: Skipped (Mobile or Email exists)");
                                continue;
                            }

                            lead = new Lead(); // Doesn't exist -> Insert
                            if (!string.IsNullOrEmpty(mobile)) existingMobiles.Add(mobile);
                            if (!string.IsNullOrEmpty(email)) existingEmails.Add(email);
                        }

                        // --- Extract Owners & Relations ---
                        string rawLeadOwnerId = GetValSafe(values, "LeadOwner.id");
                        string rawAccountOwnerId = GetValSafe(values, "AccountOwner.id");
                        string rawCoOwnerId = GetValSafe(values, "Co-Owner.id");
                        string rawDemoOwnerId = GetValSafe(values, "DemoOwner.id");
                        string rawAccountId = GetValSafe(values, "AccountName.id");

                        // --- Core Identity ---
                        lead.ZohoRecordId = zohoRecordId;
                        lead.LeadName = GetValSafe(values, "LeadName");
                        lead.EmailID = GetValSafe(values, "EmailID");
                        lead.MobileNumber = mobile;
                        lead.AlternateMobile = GetValSafe(values, "AlternateMobile");
                        lead.AlternateEmailID = GetValSafe(values, "AlternateEmailID");
                        lead.LeadOwnerId = validUserIds.Contains(rawLeadOwnerId) ? rawLeadOwnerId : null;
                        lead.AccountOwnerId = validUserIds.Contains(rawAccountOwnerId) ? rawAccountOwnerId : null;
                        lead.CoOwnerId = validUserIds.Contains(rawCoOwnerId) ? rawCoOwnerId : null;
                        lead.DemoOwnerId = validUserIds.Contains(rawDemoOwnerId) ? rawDemoOwnerId : null;

                        // MAPPING TO ACCOUNT VIA LOOKUP
                        lead.AccountId = accountLookup.TryGetValue(rawAccountId, out int accId) ? accId : null;

                        // --- Status & Pipeline ---
                        lead.Stage = GetValSafe(values, "Stage");
                        lead.Probability = GetDecimalSafe(values, "Probability(%)");
                        lead.LeadStatus = GetValSafe(values, "LeadStatus");
                        lead.CurrentStatus = GetValSafe(values, "CurrentStatus");
                        lead.Pipeline = GetValSafe(values, "Pipeline");
                        lead.DataSources = GetValSafe(values, "DataSources");
                        lead.CampaignSource = GetValSafe(values, "CampaignSource");
                        lead.MetaCampaignName = GetValSafe(values, "MetaCampaignName");
                        lead.Description = GetValSafe(values, "Description");
                        lead.Remarks = GetValSafe(values, "Remarks");
                        lead.ConversationRemarks = GetValSafe(values, "Remarks/Notes");

                        // --- Tracking & Financials ---
                        lead.Budget = GetDecimalSafe(values, "Budget");
                        lead.ExpectedRevenue = GetDecimalSafe(values, "ExpectedRevenue");
                        lead.TimePeriodToBuy = GetValSafe(values, "TimeperiodtoBuy");
                        lead.LostReason = GetValSafe(values, "LostReason");
                        lead.NotInterestedReason = GetValSafe(values, "NotInterestedReason");

                        // --- Professional Info ---
                        lead.IsHomeopathicDoctor = GetValSafe(values, "AreyouaHomopathicDoctor");
                        lead.ClinicType = GetValSafe(values, "ClinicType");
                        lead.WorkType = GetValSafe(values, "WorkType");
                        lead.HasComputer = GetValSafe(values, "HavingComputer/Laptop");
                        lead.Qualification = GetValSafe(values, "Qualification");
                        lead.YearOfPassing = GetValSafe(values, "Yearofpassing");
                        lead.CollegeName = GetValSafe(values, "CollegeName");
                        lead.Age = int.TryParse(GetValSafe(values, "Age"), out int age) ? age : null;
                        lead.YearOfPractice = int.TryParse(GetValSafe(values, "Yearofpractice"), out int yop) ? yop : null;
                        lead.TotalExperience = int.TryParse(GetValSafe(values, "Totalexperience"), out int te) ? te : null;
                        lead.AveragePatientFee = GetDecimalSafe(values, "AveragePatientFee");
                        lead.NumberOfClinics = int.TryParse(GetValSafe(values, "NumberofClinics"), out int noc) ? noc : null;
                        lead.PatientsPerDay = int.TryParse(GetValSafe(values, "PatientsperDay"), out int ppd) ? ppd : null;
                        lead.LeadProfile = GetValSafe(values, "Leadprofie");
                        lead.GroupName = GetValSafe(values, "GroupName");

                        // --- Software & Purchases ---
                        lead.CurrentlyUsingSoftware = GetValSafe(values, "CurrentlyUsingSoftware");
                        lead.CurrentSoftwareName = GetValSafe(values, "CurrentSoftwareName");
                        lead.NewSoftware = GetValSafe(values, "NewSoftware");
                        lead.RadarOpusVersion = GetValSafe(values, "RadarOpusversion");
                        lead.RadarOpusLicenseNo = GetValSafe(values, "RadarOpusLicenseno");
                        lead.ProductPackage = GetValSafe(values, "Product/Packagepackage");
                        lead.PurchaseValue = GetDecimalSafe(values, "PurchaseValue(â‚¹)");
                        lead.PaymentStatus = GetValSafe(values, "PaymentStatus");
                        lead.PaymentType = GetValSafe(values, "PaymentType");
                        lead.PaymentMode = GetValSafe(values, "PaymentMode");
                        lead.CustomerStatus = GetValSafe(values, "CustomerStatus");

                        // --- Deal Info directly on Lead ---
                        lead.DealType = GetValSafe(values, "DealType");
                        lead.DealValue = GetDecimalSafe(values, "DealValue(â‚¹)");
                        lead.Discount = GetDecimalSafe(values, "Discount");
                        lead.PackageSelected = GetValSafe(values, "PackageSelected");
                        lead.SubTotal = GetDecimalSafe(values, "SubTotal");
                        lead.Adjustment = GetDecimalSafe(values, "adjustment.");
                        lead.Taxes = GetDecimalSafe(values, "taxs.");
                        lead.GrandTotal = GetDecimalSafe(values, "grandtotal");

                        // --- Dates ---
                        lead.DateOfBirth = DateTime.TryParse(GetValSafe(values, "DateofBirth"), out DateTime dob) ? dob : null;
                        lead.CreatedDateAndTime = DateTime.TryParse(GetValSafe(values, "CreatedTime"), out DateTime cdt) ? cdt : DateTime.Now;
                        lead.ModifiedTime = DateTime.TryParse(GetValSafe(values, "ModifiedTime"), out DateTime mdt) ? mdt : null;
                        lead.LeadCreatedTime = DateTime.TryParse(GetValSafe(values, "Lead-Created-Time"), out DateTime lct) ? lct : DateTime.Now;
                        lead.DemoScheduledDate = DateTime.TryParse(GetValSafe(values, "Demoscheduleddateandtime"), out DateTime dsdt) ? dsdt : null;
                        lead.FirstCallDate = DateTime.TryParse(GetValSafe(values, "FirstcallDate"), out DateTime fcd) ? fcd : null;
                        lead.NextFollowUpDate = DateTime.TryParse(GetValSafe(values, "Nextfollow-upDate"), out DateTime nfd) ? nfd : null;
                        lead.LastContactDate = DateTime.TryParse(GetValSafe(values, "LastContactDate"), out DateTime lcd) ? lcd : null;
                        lead.PurchaseDate = DateTime.TryParse(GetValSafe(values, "PurchaseDate"), out DateTime pd) ? pd : null;
                        lead.TrialStartDate = DateOnly.TryParse(GetValSafe(values, "TrialStartDate"), out DateOnly tsd) ? tsd : null;
                        lead.TrialEndDate = DateOnly.TryParse(GetValSafe(values, "TrialEndDate"), out DateOnly ted) ? ted : null;
                        lead.CreatedBy = GetValSafe(values, "CreatedBy");
                        lead.ModifiedBy = GetValSafe(values, "ModifiedBy");

                        // --- Address 1 Mapping ---
                        // If Address 1 exists, use it. If not, fallback to generic Country/Region/City columns
                        string addr1Country = GetValSafe(values, "Address1-Country/Region");
                        lead.Addr1_Country = NormalizeCountry(!string.IsNullOrEmpty(addr1Country) ? addr1Country : GetValSafe(values, "Country/Region"));

                        string addr1Street = GetValSafe(values, "Address1-StreetAddress");
                        lead.Addr1_Street = !string.IsNullOrEmpty(addr1Street) ? addr1Street : GetValSafe(values, "StreetAddress");

                        string addr1City = GetValSafe(values, "Address1-City");
                        lead.Addr1_City = !string.IsNullOrEmpty(addr1City) ? addr1City : GetValSafe(values, "City");

                        string addr1State = GetValSafe(values, "Address1-State/Province");
                        lead.Addr1_State = !string.IsNullOrEmpty(addr1State) ? addr1State : GetValSafe(values, "State/Province");

                        string addr1Zip = GetValSafe(values, "Address1-Zip/PostalCode");
                        lead.Addr1_Zip = !string.IsNullOrEmpty(addr1Zip) ? addr1Zip : GetValSafe(values, "Zip/PostalCode");

                        string addr1Flat = GetValSafe(values, "Address1-Flat/HouseNo./Building/ApartmentName");
                        lead.Addr1_FlatHouse = !string.IsNullOrEmpty(addr1Flat) ? addr1Flat : GetValSafe(values, "Flat/HouseNo./Building/ApartmentName");

                        lead.Addr1_Coordinates = GetValSafe(values, "Address1-Latitude") + " " + GetValSafe(values, "Address1-Longitude");

                        // --- Address 2 Mapping ---
                        string addr2Country = GetValSafe(values, "Address2-Country/Region");
                        lead.Addr2_Country = NormalizeCountry(!string.IsNullOrEmpty(addr2Country) ? addr2Country : GetValSafe(values, "Country/Region2"));

                        string addr2Street = GetValSafe(values, "Address2-StreetAddress");
                        lead.Addr2_Street = !string.IsNullOrEmpty(addr2Street) ? addr2Street : GetValSafe(values, "StreetAddress2");

                        string addr2City = GetValSafe(values, "Address2-City");
                        lead.Addr2_City = !string.IsNullOrEmpty(addr2City) ? addr2City : GetValSafe(values, "City2");

                        string addr2State = GetValSafe(values, "Address2-State/Province");
                        lead.Addr2_State = !string.IsNullOrEmpty(addr2State) ? addr2State : GetValSafe(values, "State/Province2");

                        string addr2Zip = GetValSafe(values, "Address2-Zip/PostalCode");
                        lead.Addr2_Zip = !string.IsNullOrEmpty(addr2Zip) ? addr2Zip : GetValSafe(values, "Zip/PostalCode2");

                        string addr2Flat = GetValSafe(values, "Address2-Flat/HouseNo./Building/ApartmentName");
                        lead.Addr2_FlatHouse = !string.IsNullOrEmpty(addr2Flat) ? addr2Flat : GetValSafe(values, "Flat/HouseNo./Building/ApartmentName2");

                        lead.Addr2_Coordinates = GetValSafe(values, "Address2-Latitude") + " " + GetValSafe(values, "Address2-Longitude");

                        // --- Secondary Contacts ---
                        lead.ContactName = GetValSafe(values, "ContactName");
                        lead.ContactPerson1 = GetValSafe(values, "ContactPersonName1");
                        lead.ContactPerson2 = GetValSafe(values, "ContactPersonName2");
                        lead.ContactPerson3 = GetValSafe(values, "ContactPersonName3");
                        lead.Contact1Phone = GetValSafe(values, "Contact1PhoneNumber");
                        lead.Contact2Phone = GetValSafe(values, "Contact2PhoneNumber");
                        lead.Contact3Phone = GetValSafe(values, "Contact3PhoneNumber");

                        // 🚀 --- AD TRACKING --- 🚀
                        lead.SocialLeadID = GetValSafe(values, "SocialLeadID");
                        lead.Gclid = GetValSafe(values, "GCLID");
                        lead.Zcampaignid = GetValSafe(values, "ZCAMPAIGNID");
                        lead.Adgroupid = GetValSafe(values, "ADGROUPID");
                        lead.Adid = GetValSafe(values, "ADID");
                        lead.Keywordid = GetValSafe(values, "KEYWORDID");
                        lead.Keyword = GetValSafe(values, "Keyword");
                        lead.ClickType = GetValSafe(values, "ClickType");
                        lead.DeviceType = GetValSafe(values, "DeviceType");
                        lead.AdNetwork = GetValSafe(values, "AdNetwork");
                        lead.SearchPartnerNetwork = GetValSafe(values, "SearchPartnerNetwork");
                        lead.AdCampaignName = GetValSafe(values, "AdCampaignName");
                        lead.AdGroupName = GetValSafe(values, "AdGroupName");
                        lead.Ad = GetValSafe(values, "Ad");
                        lead.Gadconfigid = GetValSafe(values, "GADCONFIGID");
                        lead.AdClickDate = DateTime.TryParse(GetValSafe(values, "AdClickDate"), out DateTime acd) ? acd : null;
                        lead.CostPerClick = GetDecimalSafe(values, "CostperClick");
                        lead.CostPerConversion = GetDecimalSafe(values, "CostperConversion");
                        lead.ConversionExportedOn = DateTime.TryParse(GetValSafe(values, "ConversionExportedOn"), out DateTime ceo) ? ceo : null;
                        lead.ConversionExportStatus = GetValSafe(values, "ConversionExportStatus");
                        lead.ReasonForConversionFailure = GetValSafe(values, "ReasonforConversionFailure");

                        // Route to correct list
                        if (isUpdate) leadsToUpdate.Add(lead);
                        else leadsToInsert.Add(lead);
                    }
                }

                _context.ChangeTracker.AutoDetectChangesEnabled = false;

                // 🚀 Execute Inserts & Updates
                if (leadsToInsert.Any()) await _context.Leads.AddRangeAsync(leadsToInsert);
                if (leadsToUpdate.Any()) _context.Leads.UpdateRange(leadsToUpdate);

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
                insertedCount = leadsToInsert.Count,
                updatedCount = leadsToUpdate.Count,
                skippedCount = skippedRecords.Count
            });
        }

        // ==========================================
        // NOTIFICATION HELPER: Email Notification Only
        // ==========================================
        private async System.Threading.Tasks.Task NotifyNewOwnerAsync(string newOwnerId, Dictionary<int, string> assignedLeads)
        {
            if (string.IsNullOrEmpty(newOwnerId) || assignedLeads == null || !assignedLeads.Any()) return;

            var newOwner = await _context.Users.FindAsync(newOwnerId);
            if (newOwner == null || string.IsNullOrEmpty(newOwner.Email)) return;

            string ownerName = newOwner.fullName ?? newOwner.FirstName ?? "Team Member";
            bool isBulk = assignedLeads.Count > 1;

            string subject = isBulk
                ? $"Action Required: {assignedLeads.Count} Leads Assigned to You"
                : $"Action Required: Lead '{assignedLeads.Values.First()}' Assigned to You";

            // Build the live clickable links using Url.Action
            string leadListHtml = string.Join("", assignedLeads.Select(lead =>
            {
                string linkUrl = Url.Action("Edit", "Leads", new { id = lead.Key }, Request.Scheme);
                return $"<li style='margin-bottom: 8px;'><a href='{linkUrl}' style='color: #2563eb; text-decoration: none; font-weight: bold;'>{lead.Value}</a></li>";
            }));

            string body = $@"  <div style='font-family: Arial, sans-serif; color: #333;'>      <h3>Hi {ownerName},</h3>      <p>You have been assigned as the new owner for the following {(isBulk ? "leads" : "lead")}:</p>      <ul>{leadListHtml}</ul>      <p>Please click the link(s) above or log in to the CRM to review your new assignments.</p>      <br/>      <p><small>This is an automated notification from Radar CRM.</small></p>  </div>";

            // 1. Send Email via Resend API
            try
            {
                string apiKey = _configuration["Resend:ApiKey"];
                if (string.IsNullOrEmpty(apiKey)) throw new Exception("Resend API Key is missing in appsettings.json.");

                string senderEmail = "Radar CRM <no-reply@bjaincorp.com>";
                var emailPayload = new Dictionary<string, object>
        {
            { "from", senderEmail },
            { "to", new List<string> { newOwner.Email.Trim() } },
            { "subject", subject },
            { "html", body }
        };

                using (var client = new System.Net.Http.HttpClient())
                {
                    var requestMessage = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "https://api.resend.com/emails");
                    requestMessage.Headers.Add("Authorization", $"Bearer {apiKey}");
                    requestMessage.Content = new System.Net.Http.StringContent(System.Text.Json.JsonSerializer.Serialize(emailPayload), System.Text.Encoding.UTF8, "application/json");

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

            // 🚀 The code that was generating the "You are the new owner of..." Task has been COMPLETELY REMOVED!
            // It will no longer spawn duplicate tasks in your Task index.
        }


        private string GetVal(string[] values, int index)
        {
            if (index < values.Length) return values[index]?.Trim() ?? "";
            return "";
        }

        private string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
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
    }
}