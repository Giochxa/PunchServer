using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PunchServer.Models;
using PunchServerMVC.Data;
using PunchServerMVC.Models;
using System;
using PunchServerMVC.Models.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Microsoft.AspNetCore.Hosting;

namespace PunchServer.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IRepository _repo;
        private readonly IWebHostEnvironment _env;

        public HomeController(ILogger<HomeController> logger, IRepository repo, IWebHostEnvironment env)
        {
            _logger = logger;
            _repo = repo;
            _env = env;
        }

        private void DeletePunchImageFile(Punch punch)
        {
            if (string.IsNullOrWhiteSpace(punch.ImageUrl) || string.IsNullOrWhiteSpace(_env.WebRootPath))
                return;

            var fileName = Path.GetFileName(punch.ImageUrl);
            if (string.IsNullOrWhiteSpace(fileName))
                return;

            var imagePath = Path.Combine(_env.WebRootPath, "punch_images", fileName);
            if (System.IO.File.Exists(imagePath))
                System.IO.File.Delete(imagePath);
        }

        public IActionResult Index()
        {
            return RedirectToAction(nameof(Dashboard));
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Dashboard(int month = 0, int year = 0)
        {
            var today = DateTime.Today;
            if (month < 1 || month > 12)
                month = today.Month;

            if (year < 1 || year > 9999)
                year = today.Year;

            var selectedMonth = new DateTime(year, month, 1);
            var currentMonth = new DateTime(today.Year, today.Month, 1);
            if (selectedMonth > currentMonth)
                selectedMonth = currentMonth;

            month = selectedMonth.Month;
            year = selectedMonth.Year;

            var monthStart = selectedMonth;
            var monthEnd = new DateTime(year, month, DateTime.DaysInMonth(year, month));

            var activeTodayEmployees = GetActiveEmployeesForRange(today, today).ToList();
            var operationalMonthEmployees = GetEmployeesForRange(monthStart, monthEnd, includeInactive: true).ToList();
            var operationalScheduledMonthEmployees = operationalMonthEmployees
                .Where(e => !e.DoesNotNeedSchedule)
                .ToList();

            var missingIn = BuildMissingPunchSuggestions(operationalScheduledMonthEmployees, monthStart, monthEnd, includeMissingIn: true, includeMissingOut: false);
            var missingOut = BuildMissingPunchSuggestions(operationalScheduledMonthEmployees, monthStart, monthEnd, includeMissingIn: false, includeMissingOut: true);
            var wrongType = BuildPunchTypeChangeSuggestions(operationalScheduledMonthEmployees, monthStart, monthEnd);
            var extraPunchRows = BuildExtraPunchRows(monthStart, monthEnd, operationalMonthEmployees.Select(e => e.Id).ToHashSet());
            var currentlyInRows = BuildCurrentlyInRows(activeTodayEmployees);
            var vacationRows = BuildInVacationRows(today, activeTodayEmployees.Select(e => e.Id).ToHashSet());
            var birthdayRows = BuildBirthdayRows(today);
            var upcomingBirthdayCount = CountUpcomingBirthdaysThisWeek(today);
            var activeEmployeeRows = BuildActiveEmployeeRows(activeTodayEmployees);
            var autoPunchOutRuns = GetDashboardAutoPunchOutRuns(today);
            var activeEmployeeGenderSummary = BuildGenderSummary(activeTodayEmployees);
            var currentlyInEmployeeIds = BuildCurrentlyInRows(activeTodayEmployees)
                .Select(r => r.PrimaryText)
                .ToHashSet();
            var currentlyInEmployees = activeTodayEmployees
                .Where(e => currentlyInEmployeeIds.Contains(e.FullName))
                .ToList();
            var currentlyInGenderSummary = BuildGenderSummary(currentlyInEmployees);

            var widgets = new List<DashboardWidgetViewModel>
            {
                new DashboardWidgetViewModel
                {
                    Key = "missing-in",
                    Title = "⬅️ Missing In",
                    PeriodLabel = "Selected month",
                    Count = missingIn.Count,
                    IsError = missingIn.Count > 0,
                    ActionUrl = Url.Action(nameof(DashboardDetails), new { widget = "missing-in", month, year }) ?? "#"
                },
                new DashboardWidgetViewModel
                {
                    Key = "missing-out",
                    Title = "➡️ Missing Out",
                    PeriodLabel = "Selected month",
                    Count = missingOut.Count,
                    IsError = missingOut.Count > 0,
                    ActionUrl = Url.Action(nameof(DashboardDetails), new { widget = "missing-out", month, year }) ?? "#"
                },
                new DashboardWidgetViewModel
                {
                    Key = "extra-punches",
                    Title = "👽 Extra Punches",
                    PeriodLabel = "Selected month",
                    Count = extraPunchRows.Count,
                    IsError = extraPunchRows.Count > 0,
                    ActionUrl = Url.Action(nameof(DashboardDetails), new { widget = "extra-punches", month, year }) ?? "#"
                },
                new DashboardWidgetViewModel
                {
                    Key = "wrong-types",
                    Title = "🔁 Wrong Punch Types",
                    PeriodLabel = "Selected month",
                    Count = wrongType.Count,
                    IsError = wrongType.Count > 0,
                    ActionUrl = Url.Action(nameof(DashboardDetails), new { widget = "wrong-types", month, year }) ?? "#"
                },
                new DashboardWidgetViewModel
                {
                    Key = "auto-punch-out",
                    Title = "🤜 Auto Punch Out",
                    PeriodLabel = today.DayOfWeek == DayOfWeek.Monday ? "Fri-Sun" : "Last run",
                    Count = autoPunchOutRuns.Sum(r => r.AddedPunches.Count),
                    IsError = false,
                    Subtitle = BuildAutoPunchOutSubtitle(today, autoPunchOutRuns),
                    ActionUrl = Url.Action(nameof(DashboardDetails), new { widget = "auto-punch-out" }) ?? "#"
                },
                new DashboardWidgetViewModel
                {
                    Key = "birthdays-today",
                    Title = "🎂 Birthdays Today",
                    PeriodLabel = today.DayOfWeek == DayOfWeek.Friday ? "Fri-Sun" : "Current day",
                    Count = birthdayRows.Count,
                    IsError = false,
                    IsPositive = birthdayRows.Count > 0,
                    SecondaryInfo = $"Upcoming this week: {upcomingBirthdayCount}",
                    ActionUrl = Url.Action(nameof(DashboardDetails), new { widget = "birthdays-today" }) ?? "#"
                },
                new DashboardWidgetViewModel
                {
                    Key = "active-employees",
                    Title = "👥 Active Employees",
                    PeriodLabel = "Current day",
                    Count = activeEmployeeRows.Count,
                    IsError = false,
                    Subtitle = $"(♂ {activeEmployeeGenderSummary.MaleCount} / ♀ {activeEmployeeGenderSummary.FemaleCount})",
                    ActionUrl = Url.Action(nameof(DashboardDetails), new { widget = "active-employees" }) ?? "#"
                },
                new DashboardWidgetViewModel
                {
                    Key = "currently-in",
                    Title = "✅ Currently In",
                    PeriodLabel = "Current day",
                    Count = currentlyInRows.Count,
                    IsError = false,
                    Subtitle = $"(♂ {currentlyInGenderSummary.MaleCount} / ♀ {currentlyInGenderSummary.FemaleCount})",
                    ActionUrl = Url.Action(nameof(DashboardDetails), new { widget = "currently-in" }) ?? "#"
                },
                new DashboardWidgetViewModel
                {
                    Key = "in-vacation",
                    Title = "🌴 In Vacation",
                    PeriodLabel = "Current day",
                    Count = vacationRows.Count,
                    IsError = false,
                    ActionUrl = Url.Action(nameof(DashboardDetails), new { widget = "in-vacation" }) ?? "#"
                }
            };

            return View(new DashboardViewModel
            {
                Today = today,
                Year = year,
                Month = month,
                CurrentMonthLabel = monthStart.ToString("MMMM yyyy"),
                CanGoNextMonth = monthStart < currentMonth,
                Widgets = widgets
            });
        }

        public IActionResult DashboardDetails(string widget, int month = 0, int year = 0)
        {
            var today = DateTime.Today;
            if (month < 1 || month > 12)
                month = today.Month;

            if (year < 1 || year > 9999)
                year = today.Year;

            var selectedMonth = new DateTime(year, month, 1);
            var currentMonth = new DateTime(today.Year, today.Month, 1);
            if (selectedMonth > currentMonth)
                selectedMonth = currentMonth;

            month = selectedMonth.Month;
            year = selectedMonth.Year;

            var monthStart = selectedMonth;
            var monthEnd = new DateTime(year, month, DateTime.DaysInMonth(year, month));

            var activeTodayEmployees = GetActiveEmployeesForRange(today, today).ToList();
            var operationalMonthEmployees = GetEmployeesForRange(monthStart, monthEnd, includeInactive: true).ToList();
            var operationalScheduledMonthEmployees = operationalMonthEmployees
                .Where(e => !e.DoesNotNeedSchedule)
                .ToList();

            DashboardDetailViewModel vm = widget switch
            {
                "missing-in" => new DashboardDetailViewModel
                {
                    WidgetKey = widget,
                    Title = "Missing In",
                    Subtitle = monthStart.ToString("MMMM yyyy"),
                    IsError = true,
                    Columns = new List<string> { "Employee", "Timestamp", "Schedule", "Reason" },
                    Rows = BuildMissingPunchSuggestions(operationalScheduledMonthEmployees, monthStart, monthEnd, true, false)
                        .Select(s => new DashboardDetailRowViewModel
                        {
                            PrimaryText = s.EmployeeName,
                            SecondaryText = s.SuggestedTimestamp.ToString("dd.MM.yyyy HH:mm"),
                            TertiaryText = $"{s.ScheduleType ?? ""} ({FormatShiftRange(s.ShiftStart, s.ShiftEnd)})".Trim(),
                            QuaternaryText = s.Reason,
                            ActionText = "Calendar",
                            ActionUrl = Url.Action(nameof(Calendar), new
                            {
                                employeeId = s.EmployeeId,
                                month = s.SuggestedTimestamp.Month,
                                year = s.SuggestedTimestamp.Year,
                                showMissing = true,
                                missingFrom = s.SuggestedTimestamp.Date,
                                missingTo = s.SuggestedTimestamp.Date,
                                includeMissingIn = true,
                                includeMissingOut = false
                            })
                        })
                        .ToList()
                },
                "missing-out" => new DashboardDetailViewModel
                {
                    WidgetKey = widget,
                    Title = "Missing Out",
                    Subtitle = monthStart.ToString("MMMM yyyy"),
                    IsError = true,
                    Columns = new List<string> { "Employee", "Timestamp", "Schedule", "Reason" },
                    Rows = BuildMissingPunchSuggestions(operationalScheduledMonthEmployees, monthStart, monthEnd, false, true)
                        .Select(s => new DashboardDetailRowViewModel
                        {
                            PrimaryText = s.EmployeeName,
                            SecondaryText = s.SuggestedTimestamp.ToString("dd.MM.yyyy HH:mm"),
                            TertiaryText = $"{s.ScheduleType ?? ""} ({FormatShiftRange(s.ShiftStart, s.ShiftEnd)})".Trim(),
                            QuaternaryText = s.Reason,
                            ActionText = "Calendar",
                            ActionUrl = Url.Action(nameof(Calendar), new
                            {
                                employeeId = s.EmployeeId,
                                month = s.SuggestedTimestamp.Month,
                                year = s.SuggestedTimestamp.Year,
                                showMissing = true,
                                missingFrom = s.SuggestedTimestamp.Date,
                                missingTo = s.SuggestedTimestamp.Date,
                                includeMissingIn = false,
                                includeMissingOut = true
                            })
                        })
                        .ToList()
                },
                "extra-punches" => new DashboardDetailViewModel
                {
                    WidgetKey = widget,
                    Title = "Extra Punches",
                    Subtitle = monthStart.ToString("MMMM yyyy"),
                    IsError = true,
                    Columns = new List<string> { "Employee", "Timestamp", "Type", "Note" },
                    Rows = BuildExtraPunchRows(monthStart, monthEnd, operationalMonthEmployees.Select(e => e.Id).ToHashSet())
                },
                "wrong-types" => new DashboardDetailViewModel
                {
                    WidgetKey = widget,
                    Title = "Wrong Punch Types",
                    Subtitle = monthStart.ToString("MMMM yyyy"),
                    IsError = true,
                    Columns = new List<string> { "Employee", "Timestamp", "Change", "Reason" },
                    Rows = BuildPunchTypeChangeSuggestions(operationalScheduledMonthEmployees, monthStart, monthEnd)
                        .Select(s => new DashboardDetailRowViewModel
                        {
                            PrimaryText = s.EmployeeName,
                            SecondaryText = s.Timestamp.ToString("dd.MM.yyyy HH:mm"),
                            TertiaryText = $"{s.CurrentType} -> {s.ProposedType}",
                            QuaternaryText = s.Reason,
                            ActionText = "Calendar",
                            ActionUrl = Url.Action(nameof(Calendar), new
                            {
                                employeeId = s.EmployeeId,
                                month = s.Timestamp.Month,
                                year = s.Timestamp.Year,
                                showPunchTypeSuggestions = true,
                                missingFrom = s.Timestamp.Date,
                                missingTo = s.Timestamp.Date
                            })
                        })
                        .ToList()
                },
                "currently-in" => new DashboardDetailViewModel
                {
                    WidgetKey = widget,
                    Title = "Currently In",
                    Subtitle = today.ToString("dd.MM.yyyy"),
                    IsError = false,
                    Columns = new List<string> { "Employee", "Last Punch", "Type", "Department" },
                    Rows = BuildCurrentlyInRows(activeTodayEmployees)
                },
                "in-vacation" => new DashboardDetailViewModel
                {
                    WidgetKey = widget,
                    Title = "In Vacation",
                    Subtitle = today.ToString("dd.MM.yyyy"),
                    IsError = false,
                    FilterFrom = today.Date,
                    FilterTo = today.Date,
                    Columns = new List<string> { "Employee", "Vacation", "Range", "Notes" },
                    Rows = BuildInVacationRows(today, activeTodayEmployees.Select(e => e.Id).ToHashSet())
                },
                "active-employees" => new DashboardDetailViewModel
                {
                    WidgetKey = widget,
                    Title = "Active Employees",
                    Subtitle = today.ToString("dd.MM.yyyy"),
                    IsError = false,
                    Columns = new List<string> { "Employee", "Department", "Organisation", "Employment" },
                    Rows = BuildActiveEmployeeRows(activeTodayEmployees)
                },
                "birthdays-today" => new DashboardDetailViewModel
                {
                    WidgetKey = widget,
                    Title = "Birthdays Today",
                    Subtitle = today.DayOfWeek == DayOfWeek.Friday ? $"{today:dd.MM.yyyy} - {today.AddDays(2):dd.MM.yyyy}" : today.ToString("dd.MM.yyyy"),
                    IsError = false,
                    Columns = new List<string> { "Employee", "Birthday", "Department", "Age" },
                    Rows = BuildBirthdayRows(today)
                },
                "auto-punch-out" => BuildAutoPunchOutDetailViewModel(today),
                _ => new DashboardDetailViewModel
                {
                    WidgetKey = widget,
                    Title = "Dashboard Detail",
                    Subtitle = "Unknown widget",
                    IsError = false
                }
            };

            vm.Count = vm.Rows.Count;
            vm.Year = year;
            vm.Month = month;
            return View(vm);
        }

        private IEnumerable<Employee> GetActiveEmployeesForRange(DateTime fromDate, DateTime toDate)
        {
            return GetEmployeesForRange(fromDate, toDate, includeInactive: false);
        }

        private IEnumerable<Employee> GetEmployeesForRange(DateTime fromDate, DateTime toDate, bool includeInactive)
        {
            return _repo.GetEmployees()
                .Where(e => includeInactive || e.IsActive)
                .Where(e => !e.EmploymentStartDate.HasValue || e.EmploymentStartDate.Value.Date <= toDate.Date)
                .Where(e => !e.EmploymentEndDate.HasValue || e.EmploymentEndDate.Value.Date >= fromDate.Date)
                .OrderBy(e => e.FullName);
        }

        private static string FormatShiftRange(TimeSpan? start, TimeSpan? end)
        {
            if (!start.HasValue && !end.HasValue)
                return string.Empty;

            var startText = start?.ToString() ?? string.Empty;
            var endText = end?.ToString() ?? string.Empty;
            return $"{startText} - {endText}".Trim();
        }

        private List<DashboardDetailRowViewModel> BuildExtraPunchRows(DateTime monthStart, DateTime monthEnd, HashSet<int> employeeIds)
        {
            var calendarEntries = _repo.GetCalendarEntriesForMonth(monthStart.Year, monthStart.Month, null, null, null);
            var extraEntries = FilterToExtraPunches(calendarEntries)
                .SelectMany(kvp => kvp.Value)
                .Where(e => e.IsPunch && e.PunchId.HasValue && e.EmployeeId.HasValue && employeeIds.Contains(e.EmployeeId.Value))
                .GroupBy(e => e.PunchId!.Value)
                .Select(g => g.First())
                .OrderBy(e => e.EmployeeName)
                .ThenBy(e => e.PunchTime)
                .ToList();

            return extraEntries
                .Select(e => new DashboardDetailRowViewModel
                {
                    PrimaryText = e.EmployeeName,
                    SecondaryText = e.PunchTime.ToString("dd.MM.yyyy HH:mm"),
                    TertiaryText = e.PunchType,
                    QuaternaryText = e.Note ?? string.Empty
                })
                .ToList();
        }

        private List<DashboardDetailRowViewModel> BuildCurrentlyInRows(IEnumerable<Employee> employees)
        {
            var employeeList = employees.ToList();
            var employeeIds = employeeList.Select(e => e.Id).ToHashSet();
            var employeeById = employeeList.ToDictionary(e => e.Id);
            var departmentById = _repo.GetDepartments().ToDictionary(d => d.Id, d => d.Name);

            var latestPunches = _repo.GetPunches()
                .Where(p => employeeIds.Contains(p.EmployeeId))
                .Where(p => p.Timestamp >= DateTime.Today.AddDays(-1))
                .GroupBy(p => p.EmployeeId)
                .Select(g => g.OrderByDescending(p => p.Timestamp).First())
                .Where(p => string.Equals(p.PunchType, "In", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => employeeById[p.EmployeeId].FullName)
                .ToList();

            return latestPunches
                .Select(p => new DashboardDetailRowViewModel
                {
                    PrimaryText = employeeById[p.EmployeeId].FullName,
                    SecondaryText = p.Timestamp.ToString("dd.MM.yyyy HH:mm"),
                    TertiaryText = p.PunchType ?? string.Empty,
                    QuaternaryText = employeeById[p.EmployeeId].DepartmentId.HasValue &&
                                     departmentById.TryGetValue(employeeById[p.EmployeeId].DepartmentId!.Value, out var departmentName)
                        ? departmentName
                        : string.Empty
                })
                .ToList();
        }

        private List<DashboardDetailRowViewModel> BuildInVacationRows(DateTime date, HashSet<int> employeeIds)
        {
            var employeesById = _repo.GetEmployees()
                .Where(e => employeeIds.Contains(e.Id))
                .ToDictionary(e => e.Id);
            var vacationTypesById = _repo.GetVacationTypes().ToDictionary(v => v.Id, v => v);

            return _repo.GetVacationsInRange(date, date)
                .Where(v => employeeIds.Contains(v.EmployeeId) && v.IsActive)
                .OrderBy(v => employeesById.ContainsKey(v.EmployeeId) ? employeesById[v.EmployeeId].FullName : string.Empty)
                .Select(v => new DashboardDetailRowViewModel
                {
                    PrimaryText = employeesById.TryGetValue(v.EmployeeId, out var employee) ? employee.FullName : $"Employee #{v.EmployeeId}",
                    SecondaryText = vacationTypesById.TryGetValue(v.VacationTypeId, out var vacationType)
                        ? $"{vacationType.Name} ({vacationType.Abbreviation})"
                        : "Vacation",
                    TertiaryText = $"{v.StartDate:dd.MM.yyyy} - {v.EndDate:dd.MM.yyyy}",
                    QuaternaryText = v.Notes ?? string.Empty
                })
                .ToList();
        }

        private List<DashboardDetailRowViewModel> BuildActiveEmployeeRows(IEnumerable<Employee> employees)
        {
            var departmentsById = _repo.GetDepartments().ToDictionary(d => d.Id, d => d.Name);
            var organisationsById = _repo.GetOrganisations().ToDictionary(o => o.Id, o => o.Name);

            return employees
                .OrderBy(e => e.FullName)
                .Select(e => new DashboardDetailRowViewModel
                {
                    PrimaryText = e.FullName,
                    SecondaryText = e.DepartmentId.HasValue && departmentsById.TryGetValue(e.DepartmentId.Value, out var deptName) ? deptName : string.Empty,
                    TertiaryText = e.OrganisationId.HasValue && organisationsById.TryGetValue(e.OrganisationId.Value, out var orgName) ? orgName : string.Empty,
                    QuaternaryText = $"{(e.EmploymentStartDate?.ToString("dd.MM.yyyy") ?? "-")} - {(e.EmploymentEndDate?.ToString("dd.MM.yyyy") ?? "")}".TrimEnd(' ', '-')
                })
                .ToList();
        }

        private List<DashboardDetailRowViewModel> BuildBirthdayRows(DateTime today)
        {
            var birthdayDates = new HashSet<(int Month, int Day)>
            {
                (today.Month, today.Day)
            };

            if (today.DayOfWeek == DayOfWeek.Friday)
            {
                birthdayDates.Add((today.AddDays(1).Month, today.AddDays(1).Day));
                birthdayDates.Add((today.AddDays(2).Month, today.AddDays(2).Day));
            }

            var employeeWindowEnd = today.DayOfWeek == DayOfWeek.Friday ? today.AddDays(2) : today;
            var birthdayDateByMonthDay = EachDate(today.Date, employeeWindowEnd.Date)
                .ToDictionary(d => (d.Month, d.Day), d => d);
            var departmentsById = _repo.GetDepartments().ToDictionary(d => d.Id, d => d.Name);

            return GetActiveEmployeesForRange(today, employeeWindowEnd)
                .Where(e => e.BirthDate.HasValue && birthdayDates.Contains((e.BirthDate.Value.Month, e.BirthDate.Value.Day)))
                .OrderBy(e => e.FullName)
                .Select(e =>
                {
                    var birthDate = e.BirthDate!.Value;
                    birthdayDateByMonthDay.TryGetValue((birthDate.Month, birthDate.Day), out var birthdayDate);

                    return new DashboardDetailRowViewModel
                    {
                        PrimaryText = e.FullName,
                        SecondaryText = birthDate.ToString("dd.MM"),
                        TertiaryText = e.DepartmentId.HasValue && departmentsById.TryGetValue(e.DepartmentId.Value, out var deptName) ? deptName : string.Empty,
                        QuaternaryText = CalculateAgeOnBirthday(birthDate, birthdayDate == default ? today.Year : birthdayDate.Year).ToString()
                    };
                })
                .ToList();
        }

        private static int CalculateAgeOnBirthday(DateTime birthDate, int year)
        {
            return Math.Max(0, year - birthDate.Year);
        }

        private int CountUpcomingBirthdaysThisWeek(DateTime today)
        {
            var daysUntilSunday = ((int)DayOfWeek.Sunday - (int)today.DayOfWeek + 7) % 7;
            if (daysUntilSunday == 0)
                return 0;

            var fromDate = today.Date.AddDays(1);
            var toDate = today.Date.AddDays(daysUntilSunday);
            var birthdayDates = EachDate(fromDate, toDate)
                .Select(d => (d.Month, d.Day))
                .ToHashSet();

            return GetActiveEmployeesForRange(fromDate, toDate)
                .Count(e => e.BirthDate.HasValue && birthdayDates.Contains((e.BirthDate.Value.Month, e.BirthDate.Value.Day)));
        }

        private List<AutoPunchOutRunLog> GetDashboardAutoPunchOutRuns(DateTime today)
        {
            if (today.DayOfWeek != DayOfWeek.Monday)
            {
                return _repo.GetAutoPunchOutRunLogs(1).ToList();
            }

            var fromDate = today.Date.AddDays(-3);
            var toDate = today.Date.AddDays(-1);

            return _repo.GetAutoPunchOutRunLogs(50)
                .Where(r => r.FinishedAt.Date >= fromDate && r.FinishedAt.Date <= toDate)
                .OrderByDescending(r => r.FinishedAt)
                .ToList();
        }

        private static string BuildAutoPunchOutSubtitle(DateTime today, List<AutoPunchOutRunLog> runLogs)
        {
            if (runLogs.Count == 0)
                return today.DayOfWeek == DayOfWeek.Monday ? "No runs for Fri-Sun" : "Never ran";

            if (today.DayOfWeek != DayOfWeek.Monday)
            {
                var lastRun = runLogs.First();
                return $"{lastRun.Status} at {lastRun.FinishedAt:dd.MM.yyyy HH:mm}";
            }

            var friday = today.Date.AddDays(-3);
            var saturday = today.Date.AddDays(-2);
            var sunday = today.Date.AddDays(-1);

            int CountFor(DateTime date) => runLogs
                .Where(r => r.FinishedAt.Date == date.Date)
                .Sum(r => r.AddedPunches.Count);

            return $"Fri {CountFor(friday)} / Sat {CountFor(saturday)} / Sun {CountFor(sunday)}";
        }

        private DashboardDetailViewModel BuildAutoPunchOutDetailViewModel(DateTime today)
        {
            var runLogs = GetDashboardAutoPunchOutRuns(today);
            var filterFrom = today.DayOfWeek == DayOfWeek.Monday ? today.Date.AddDays(-3) : today.Date;
            var filterTo = today.DayOfWeek == DayOfWeek.Monday ? today.Date.AddDays(-1) : today.Date;

            if (runLogs.Count == 0)
            {
                return new DashboardDetailViewModel
                {
                    WidgetKey = "auto-punch-out",
                    Title = "Auto Punch Out",
                    Subtitle = today.DayOfWeek == DayOfWeek.Monday ? "No runs for Fri-Sun" : "No runs yet",
                    IsError = false,
                    FilterFrom = filterFrom,
                    FilterTo = filterTo,
                    Columns = new List<string> { "Employee", "Punch Out", "Work Date", "Schedule" },
                    Rows = new List<DashboardDetailRowViewModel>()
                };
            }

            if (today.DayOfWeek != DayOfWeek.Monday)
            {
                var punchDates = runLogs
                    .SelectMany(r => r.AddedPunches)
                    .Select(p => p.PunchTimestamp.Date)
                    .ToList();

                var fallbackDate = runLogs.First().FinishedAt.Date;
                filterFrom = punchDates.Count > 0 ? punchDates.Min() : fallbackDate;
                filterTo = punchDates.Count > 0 ? punchDates.Max() : fallbackDate;
            }

            var subtitle = today.DayOfWeek == DayOfWeek.Monday
                ? $"{today.AddDays(-3):dd.MM.yyyy} - {today.AddDays(-1):dd.MM.yyyy} | {BuildAutoPunchOutSubtitle(today, runLogs)}"
                : $"{runLogs.First().Status} | Finished {runLogs.First().FinishedAt:dd.MM.yyyy HH:mm} | {runLogs.First().Message ?? string.Empty}".Trim().TrimEnd('|').Trim();

            return new DashboardDetailViewModel
            {
                WidgetKey = "auto-punch-out",
                Title = "Auto Punch Out",
                Subtitle = subtitle,
                IsError = false,
                FilterFrom = filterFrom,
                FilterTo = filterTo,
                Columns = new List<string> { "Employee", "Punch Out", "Work Date", "Schedule" },
                Rows = runLogs
                    .SelectMany(r => r.AddedPunches)
                    .OrderBy(p => p.EmployeeName)
                    .ThenBy(p => p.PunchTimestamp)
                    .Select(p => new DashboardDetailRowViewModel
                    {
                        PrimaryText = p.EmployeeName,
                        SecondaryText = p.PunchTimestamp.ToString("dd.MM.yyyy HH:mm"),
                        TertiaryText = p.WorkDate.ToString("dd.MM.yyyy"),
                        QuaternaryText = p.ScheduleText
                    })
                    .ToList()
            };
        }

        private static (int MaleCount, int FemaleCount) BuildGenderSummary(IEnumerable<Employee> employees)
        {
            int male = 0;
            int female = 0;

            foreach (var employee in employees)
            {
                var gender = employee.Gender?.Trim();
                if (string.IsNullOrWhiteSpace(gender))
                    continue;

                if (gender.Contains("მამრ", StringComparison.OrdinalIgnoreCase) ||
                    gender.Contains("áƒ›áƒáƒ›áƒ ", StringComparison.OrdinalIgnoreCase))
                {
                    male++;
                }
                else if (gender.Contains("მდედ", StringComparison.OrdinalIgnoreCase) ||
                         gender.Contains("áƒ›áƒ“áƒ”", StringComparison.OrdinalIgnoreCase))
                {
                    female++;
                }
            }

            return (male, female);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }

        // Simple manual punch endpoint used by Index view (separate from kiosk API)
        [HttpPost]
        public IActionResult Punch(int employeeId, string punchType, string? note = null)
        {
            _repo.AddPunch(new Punch
            {
                EmployeeId = employeeId,
                PunchType = punchType,
                Timestamp = DateTime.Now,
                CreatedAt = DateTime.Now,
                IsManualCreated = true,
                IsManualEdited = false,
                UpdatedAt = null,
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
            });

            ViewBag.Message = "Punch recorded!";
            return View("Index");
        }

        // -----------------------------
        // Missing punch suggestions (shown on Calendar page)
        // -----------------------------
        public sealed class MissingPunchSuggestion
        {
            // deterministic key used for checkbox selection
            public string Key { get; set; } = "";
            public int EmployeeId { get; set; }
            public string EmployeeName { get; set; } = "";
            public int? DepartmentId { get; set; }
            public string MissingType { get; set; } = ""; // "In" or "Out"
            public DateTime SuggestedTimestamp { get; set; }
            public string Reason { get; set; } = "";
            public string? ScheduleType { get; set; }
            public TimeSpan? ShiftStart { get; set; }
            public TimeSpan? ShiftEnd { get; set; }
        }

        public sealed class PunchTypeChangeSuggestion
        {
            public int PunchId { get; set; }
            public int EmployeeId { get; set; }
            public string EmployeeName { get; set; } = "";
            public DateTime Timestamp { get; set; }
            public string CurrentType { get; set; } = "";
            public string ProposedType { get; set; } = "";
            public string Reason { get; set; } = "";
            public string? ScheduleType { get; set; }
            public TimeSpan? ShiftStart { get; set; }
            public TimeSpan? ShiftEnd { get; set; }
        }

        private static IEnumerable<DateTime> EachDate(DateTime from, DateTime toInclusive)
        {
            for (var d = from.Date; d <= toInclusive.Date; d = d.AddDays(1))
                yield return d;
        }

        private static string NormType(string? t) => (t ?? "").Trim();

        private static (DateTime shiftStart, DateTime shiftEnd) GetShiftWindow(DateTime date, Schedule s)
        {
            var start = date.Date + s.ShiftStart;
            var isNight = s.ShiftEnd < s.ShiftStart;
            var end = isNight ? date.Date.AddDays(1) + s.ShiftEnd : date.Date + s.ShiftEnd;
            return (start, end);
        }

        private static bool HasPunchOfTypeInRange(IEnumerable<Punch> punches, string punchType, DateTime from, DateTime to)
        {
            return punches.Any(p =>
                NormType(p.PunchType).Equals(punchType, StringComparison.OrdinalIgnoreCase) &&
                p.Timestamp >= from && p.Timestamp <= to);
        }

        private static bool HasPunchNear(IEnumerable<Punch> punches, string punchType, DateTime ts, TimeSpan tolerance)
        {
            var from = ts - tolerance;
            var to = ts + tolerance;
            return HasPunchOfTypeInRange(punches, punchType, from, to);
        }

        private static bool IsAutoPunch(Punch p)
        {
            return p.IsManualCreated == false && p.IsManualEdited == false;
        }

        private static DateTime GetEffectiveWorkDate(Schedule s, DateTime ts)
        {
            bool isNightShift = s.ShiftEnd < s.ShiftStart;

            if (!isNightShift)
                return ts.Date;

            var gap = s.ShiftStart - s.ShiftEnd;
            if (gap < TimeSpan.Zero)
                gap = gap.Add(TimeSpan.FromDays(1));

            var cutoff = s.ShiftEnd + TimeSpan.FromTicks(gap.Ticks / 2);

            if (ts.TimeOfDay < cutoff)
                return ts.AddDays(-1).Date;

            return ts.Date;
        }

        private static bool IsScheduleMatchForTimestamp(Schedule s, DateTime ts)
        {
            var effectiveDate = GetEffectiveWorkDate(s, ts);
            var effectiveDay = effectiveDate.DayOfWeek;

            if (s.Days != null && s.Days.Count > 0 && !s.Days.Contains(effectiveDay))
                return false;

            if (s.StartDate.Date > effectiveDate) return false;
            if (s.EndDate.Date < effectiveDate) return false;

            return true;
        }

        private static Dictionary<int, (string proposedType, string reason)> BuildProposedTypeMap(
            Schedule schedule,
            List<Punch> punches)
        {
            var map = new Dictionary<int, (string proposedType, string reason)>();
            bool isNightShift = schedule.ShiftEnd < schedule.ShiftStart;

            foreach (var p in punches.OrderBy(x => x.Timestamp))
            {
                string proposedType;
                string reason;

                if (!isNightShift)
                {
                    var shiftLength = schedule.ShiftEnd - schedule.ShiftStart;
                    var midpoint = schedule.ShiftStart.Add(TimeSpan.FromTicks(shiftLength.Ticks / 2));

                    if (p.Timestamp.TimeOfDay < midpoint)
                    {
                        proposedType = "In";
                        reason = $"Day shift: before midpoint {midpoint:hh\\:mm}";
                    }
                    else
                    {
                        proposedType = "Out";
                        reason = $"Day shift: from midpoint {midpoint:hh\\:mm}";
                    }
                }
                else
                {
                    var gap = schedule.ShiftStart - schedule.ShiftEnd;
                    if (gap < TimeSpan.Zero)
                        gap = gap.Add(TimeSpan.FromDays(1));

                    var cutoff = schedule.ShiftEnd + TimeSpan.FromTicks(gap.Ticks / 2);

                    if (p.Timestamp.TimeOfDay < cutoff)
                    {
                        proposedType = "Out";
                        reason = $"Night shift: morning side (< {cutoff:hh\\:mm})";
                    }
                    else
                    {
                        proposedType = "In";
                        reason = $"Night shift: evening side (>= {cutoff:hh\\:mm})";
                    }
                }

                map[p.Id] = (proposedType, reason);
            }

            return map;
        }

        private HashSet<DateTime> GetHolidayDateSet(DateTime fromDate, DateTime toDate)
        {
            return _repo.GetHolidaysInRange(fromDate, toDate)
                .Select(h => h.Date.Date)
                .ToHashSet();
        }

        private Dictionary<int, HashSet<DateTime>> GetVacationDatesByEmployee(HashSet<int> employeeIds, DateTime fromDate, DateTime toDate)
        {
            var result = new Dictionary<int, HashSet<DateTime>>();

            var vacations = _repo.GetVacationsInRange(fromDate, toDate)
                .Where(v => employeeIds.Contains(v.EmployeeId))
                .ToList();

            foreach (var vacation in vacations)
            {
                var start = vacation.StartDate.Date < fromDate.Date ? fromDate.Date : vacation.StartDate.Date;
                var end = vacation.EndDate.Date > toDate.Date ? toDate.Date : vacation.EndDate.Date;

                if (!result.TryGetValue(vacation.EmployeeId, out var dates))
                {
                    dates = new HashSet<DateTime>();
                    result[vacation.EmployeeId] = dates;
                }

                foreach (var day in EachDate(start, end))
                    dates.Add(day.Date);
            }

            return result;
        }

        private static bool TryParseMissingSuggestionKey(string key, out int employeeId, out string type, out DateTime timestamp)
        {
            employeeId = 0;
            type = string.Empty;
            timestamp = DateTime.MinValue;

            var parts = (key ?? string.Empty).Split('|');
            if (parts.Length < 3)
                return false;

            if (!int.TryParse(parts[0], out employeeId))
                return false;

            type = parts[1];
            if (string.IsNullOrWhiteSpace(type))
                return false;

            if (!long.TryParse(parts[2], out var ticks))
                return false;

            try
            {
                timestamp = new DateTime(ticks);
                return true;
            }
            catch
            {
                employeeId = 0;
                type = string.Empty;
                timestamp = DateTime.MinValue;
                return false;
            }
        }

        private List<(DateTime startDate, DateTime endDate)> BuildDateRanges(IEnumerable<DateTime> dates)
        {
            var orderedDates = dates
                .Select(d => d.Date)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            var ranges = new List<(DateTime startDate, DateTime endDate)>();
            if (orderedDates.Count == 0)
                return ranges;

            var rangeStart = orderedDates[0];
            var rangeEnd = orderedDates[0];

            for (var i = 1; i < orderedDates.Count; i++)
            {
                var current = orderedDates[i];
                if (current == rangeEnd.AddDays(1))
                {
                    rangeEnd = current;
                    continue;
                }

                ranges.Add((rangeStart, rangeEnd));
                rangeStart = current;
                rangeEnd = current;
            }

            ranges.Add((rangeStart, rangeEnd));
            return ranges;
        }

        private List<Employee> GetEmployeesWithEffectivePhoto(IEnumerable<int>? relevantEmployeeIds = null)
        {
            var employees = _repo.GetEmployees().ToList();
            var employeeIdsToResolve = (relevantEmployeeIds ?? employees.Select(e => e.Id))
                .Distinct()
                .ToHashSet();

            var employeesMissingPhotos = employees
                .Where(e => employeeIdsToResolve.Contains(e.Id) && string.IsNullOrWhiteSpace(e.PhotoUrl))
                .Select(e => e.Id)
                .ToList();

            var firstPunchImages = _repo.GetFirstPunchImagesForEmployees(employeesMissingPhotos);

            foreach (var employee in employees)
            {
                if (string.IsNullOrWhiteSpace(employee.PhotoUrl) &&
                    firstPunchImages.TryGetValue(employee.Id, out var imageUrl) &&
                    !string.IsNullOrWhiteSpace(imageUrl))
                {
                    employee.PhotoUrl = imageUrl;
                }
            }

            return employees;
        }

        private List<MissingPunchSuggestion> BuildMissingPunchSuggestions(
    IEnumerable<Employee> employees,
    DateTime fromDate,
    DateTime toDate,
    bool includeMissingIn,
    bool includeMissingOut)
{
    var suggestions = new List<MissingPunchSuggestion>();
    var now = DateTime.Now;
    var holidayDates = GetHolidayDateSet(fromDate, toDate);

    var employeeIds = employees.Select(e => e.Id).ToHashSet();
    var vacationDatesByEmployee = GetVacationDatesByEmployee(employeeIds, fromDate, toDate);

    // Pull punches once for the whole period (+1 day for night shifts)
    var punchWindowFrom = fromDate.Date.AddDays(-1);
    var punchWindowToExclusive = toDate.Date.AddDays(2);

    var allPunches = _repo.GetPunches()
        .Where(p => employeeIds.Contains(p.EmployeeId))
        .Where(p => p.Timestamp >= punchWindowFrom && p.Timestamp < punchWindowToExclusive)
        .OrderBy(p => p.Timestamp)
        .ToList();

    var punchesByEmployee = allPunches
        .GroupBy(p => p.EmployeeId)
        .ToDictionary(g => g.Key, g => g.ToList());

    var schedules = _repo.GetSchedules()
        .Where(s => employeeIds.Contains(s.EmployeeId))
        .ToList();

    // Tolerances/windows
    var nearTolerance = TimeSpan.FromMinutes(5);   // don't suggest if already exists near suggested time
    var administrationSettings = _repo.GetAdministrationSettings();
    var missingInLookbackMinutes = administrationSettings.MissingInLookbackMinutes > 0
        ? administrationSettings.MissingInLookbackMinutes
        : 120;
    var windowPadBefore = TimeSpan.FromMinutes(missingInLookbackMinutes);   // before shift start
    var windowPadAfterOut = TimeSpan.FromHours(6);    // after shift end (late OUT tolerance)

        foreach (var emp in employees)
        {
            punchesByEmployee.TryGetValue(emp.Id, out var empPunches);
            empPunches ??= new List<Punch>();

            var empSchedules = schedules
                .Where(s => s.EmployeeId == emp.Id)
                .Where(s => s.StartDate.Date <= toDate.Date && s.EndDate.Date >= fromDate.Date.AddDays(-1))
                .ToList();

        if (empSchedules.Count == 0)
            continue; // no schedule => don't guess

        foreach (var sched in empSchedules)
        {
            var scheduleScanFrom = sched.ShiftEnd < sched.ShiftStart
                ? fromDate.Date.AddDays(-1)
                : fromDate.Date;

            var activeFrom = sched.StartDate.Date < scheduleScanFrom ? scheduleScanFrom : sched.StartDate.Date;
            var activeTo = sched.EndDate.Date > toDate.Date ? toDate.Date : sched.EndDate.Date;

            vacationDatesByEmployee.TryGetValue(emp.Id, out var empVacationDates);

            foreach (var day in EachDate(activeFrom, activeTo))
            {
                if (holidayDates.Contains(day.Date))
                    continue;

                if (empVacationDates != null && empVacationDates.Contains(day.Date))
                    continue;

                // If Days list is empty, treat as all days (common for older LiteDB data)
                if (sched.Days != null && sched.Days.Count > 0 && !sched.Days.Contains(day.DayOfWeek))
                    continue;

                var (shiftStart, shiftEnd) = GetShiftWindow(day, sched);

                var windowStart = shiftStart - windowPadBefore;
                var windowEnd = shiftEnd + windowPadAfterOut;

                var punchesInShift = empPunches
                    .Where(p => p.Timestamp >= windowStart && p.Timestamp <= windowEnd)
                    .ToList();

                // ✅ FIX: Do NOT compare "near start/end". Late/Early punches still count as present.
                bool hasInInWindow = punchesInShift.Any(p =>
                    string.Equals(p.PunchType, "In", StringComparison.OrdinalIgnoreCase));

                bool hasOutInWindow = punchesInShift.Any(p =>
                    string.Equals(p.PunchType, "Out", StringComparison.OrdinalIgnoreCase));

                var hasAnyPunch = punchesInShift.Any();

                // If there are no punches at all inside the shift window => BOTH missing
                if (!hasAnyPunch)
                {
                    if (includeMissingIn)
                    {
                        if (shiftStart.Date >= fromDate.Date && shiftStart.Date <= toDate.Date &&
                            shiftStart <= now &&
                            !HasPunchNear(punchesInShift, "In", shiftStart, nearTolerance))
                        {
                            var key = $"{emp.Id}|In|{shiftStart.Ticks}|0";
                            suggestions.Add(new MissingPunchSuggestion
                            {
                                Key = key,
                                EmployeeId = emp.Id,
                                EmployeeName = emp.FullName,
                                DepartmentId = emp.DepartmentId,
                                MissingType = "In",
                                SuggestedTimestamp = shiftStart,
                                Reason = "No punches found in shift window (missing IN & OUT)",
                                ScheduleType = sched.ScheduleType,
                                ShiftStart = sched.ShiftStart,
                                ShiftEnd = sched.ShiftEnd
                            });
                        }
                    }

                    if (includeMissingOut)
                    {
                        // For night shift, OUT date can be next day; allow it if shiftEnd is within range
                        if (shiftEnd.Date >= fromDate.Date && shiftEnd.Date <= toDate.Date &&
                            shiftEnd <= now &&
                            !HasPunchNear(punchesInShift, "Out", shiftEnd, nearTolerance))
                        {
                            var key = $"{emp.Id}|Out|{shiftEnd.Ticks}|0";
                            suggestions.Add(new MissingPunchSuggestion
                            {
                                Key = key,
                                EmployeeId = emp.Id,
                                EmployeeName = emp.FullName,
                                DepartmentId = emp.DepartmentId,
                                MissingType = "Out",
                                SuggestedTimestamp = shiftEnd,
                                Reason = "No punches found in shift window (missing IN & OUT)",
                                ScheduleType = sched.ScheduleType,
                                ShiftStart = sched.ShiftStart,
                                ShiftEnd = sched.ShiftEnd
                            });
                        }
                    }

                    continue;
                }

                // ✅ Missing OUT only if there is NO OUT anywhere in the shift window
                if (includeMissingOut && !hasOutInWindow)
                {
                    var suggested = shiftEnd;
                    if (suggested.Date >= fromDate.Date && suggested.Date <= toDate.Date &&
                        suggested <= now)
                    {
                        if (!HasPunchNear(punchesInShift, "Out", suggested, nearTolerance))
                        {
                            var key = $"{emp.Id}|Out|{suggested.Ticks}|0";
                            suggestions.Add(new MissingPunchSuggestion
                            {
                                Key = key,
                                EmployeeId = emp.Id,
                                EmployeeName = emp.FullName,
                                DepartmentId = emp.DepartmentId,
                                MissingType = "Out",
                                SuggestedTimestamp = suggested,
                                Reason = "Missing OUT in shift window (day/night aware)",
                                ScheduleType = sched.ScheduleType,
                                ShiftStart = sched.ShiftStart,
                                ShiftEnd = sched.ShiftEnd
                            });
                        }
                    }
                }

                // ✅ Missing IN only if there is NO IN anywhere in the shift window
                if (includeMissingIn && !hasInInWindow)
                {
                    var suggested = shiftStart;
                    if (suggested.Date >= fromDate.Date && suggested.Date <= toDate.Date &&
                        suggested <= now)
                    {
                        if (!HasPunchNear(punchesInShift, "In", suggested, nearTolerance))
                        {
                            var key = $"{emp.Id}|In|{suggested.Ticks}|0";
                            suggestions.Add(new MissingPunchSuggestion
                            {
                                Key = key,
                                EmployeeId = emp.Id,
                                EmployeeName = emp.FullName,
                                DepartmentId = emp.DepartmentId,
                                MissingType = "In",
                                SuggestedTimestamp = suggested,
                                Reason = "Missing IN in shift window (day/night aware)",
                                ScheduleType = sched.ScheduleType,
                                ShiftStart = sched.ShiftStart,
                                ShiftEnd = sched.ShiftEnd
                            });
                        }
                    }
                }
            }
        }
    }

    return suggestions
        .OrderBy(s => s.EmployeeName)
        .ThenBy(s => s.SuggestedTimestamp)
        .ToList();
}

        private List<PunchTypeChangeSuggestion> BuildPunchTypeChangeSuggestions(
            IEnumerable<Employee> employees,
            DateTime fromDate,
            DateTime toDate)
        {
            var employeeIds = employees.Select(e => e.Id).ToHashSet();
            var holidayDates = GetHolidayDateSet(fromDate, toDate);
            var vacationDatesByEmployee = GetVacationDatesByEmployee(employeeIds, fromDate, toDate);
            var employeesById = employees.ToDictionary(e => e.Id);

            var punchWindowFrom = fromDate.Date.AddDays(-1);
            var punchWindowToExclusive = toDate.Date.AddDays(2);

            var punches = _repo.GetPunches()
                .Where(p => employeeIds.Contains(p.EmployeeId))
                .Where(p => p.Timestamp >= punchWindowFrom && p.Timestamp < punchWindowToExclusive)
                .ToList();

            var schedules = _repo.GetSchedules()
                .Where(s => employeeIds.Contains(s.EmployeeId))
                .Where(s => s.StartDate.Date <= toDate.Date && s.EndDate.Date >= fromDate.Date)
                .ToList();

            var suggestions = new List<PunchTypeChangeSuggestion>();

            foreach (var schedule in schedules)
            {
                if (!employeesById.TryGetValue(schedule.EmployeeId, out var employee))
                    continue;

                vacationDatesByEmployee.TryGetValue(schedule.EmployeeId, out var empVacationDates);

                var schedulePunches = punches
                    .Where(p => p.EmployeeId == schedule.EmployeeId)
                    .Where(IsAutoPunch)
                    .Where(p => IsScheduleMatchForTimestamp(schedule, p.Timestamp))
                    .Where(p =>
                    {
                        var effectiveDate = GetEffectiveWorkDate(schedule, p.Timestamp).Date;
                        if (effectiveDate < fromDate.Date || effectiveDate > toDate.Date)
                            return false;
                        if (holidayDates.Contains(effectiveDate))
                            return false;
                        if (empVacationDates != null && empVacationDates.Contains(effectiveDate))
                            return false;
                        return true;
                    })
                    .ToList();

                if (schedulePunches.Count == 0)
                    continue;

                var proposedMap = BuildProposedTypeMap(schedule, schedulePunches);

                foreach (var punch in schedulePunches.OrderBy(p => p.Timestamp))
                {
                    var currentType = punch.PunchType ?? string.Empty;
                    if (!proposedMap.TryGetValue(punch.Id, out var proposed))
                        continue;

                    if (string.Equals(currentType, proposed.proposedType, StringComparison.OrdinalIgnoreCase))
                        continue;

                    suggestions.Add(new PunchTypeChangeSuggestion
                    {
                        PunchId = punch.Id,
                        EmployeeId = punch.EmployeeId,
                        EmployeeName = employee.FullName,
                        Timestamp = punch.Timestamp,
                        CurrentType = currentType,
                        ProposedType = proposed.proposedType,
                        Reason = proposed.reason,
                        ScheduleType = schedule.ScheduleType,
                        ShiftStart = schedule.ShiftStart,
                        ShiftEnd = schedule.ShiftEnd
                    });
                }
            }

            return suggestions
                .OrderBy(s => s.EmployeeName)
                .ThenBy(s => s.Timestamp)
                .ToList();
        }

        private static Dictionary<DateTime, List<CalendarEntry>> FilterToExtraPunches(
            Dictionary<DateTime, List<CalendarEntry>> calendarEntries)
        {
            var extraPunchIds = new HashSet<int>();

            foreach (var dayEntry in calendarEntries)
            {
                foreach (var employeeGroup in dayEntry.Value
                    .Where(e => e.EmployeeId.HasValue)
                    .GroupBy(e => e.EmployeeId!.Value))
                {
                    var punches = employeeGroup
                        .Where(e => e.IsPunch && e.PunchId.HasValue)
                        .OrderBy(e => e.PunchTime)
                        .ToList();

                    if (punches.Count == 0)
                        continue;

                    var schedules = employeeGroup
                        .Where(e => !e.IsPunch && !e.IsVacation)
                        .ToList();

                    if (schedules.Count == 0)
                    {
                        foreach (var punchWithoutSchedule in punches)
                        {
                            if (punchWithoutSchedule.PunchId.HasValue)
                                extraPunchIds.Add(punchWithoutSchedule.PunchId.Value);
                        }

                        continue;
                    }

                    var hasNightShiftContinuation = schedules.Any(s => s.IsNightShift && s.IsScheduleContinuation);
                    var hasNightShiftStart = schedules.Any(s => s.IsNightShift && !s.IsScheduleContinuation);

                    var threshold =
                        hasNightShiftContinuation && hasNightShiftStart ? 2 :
                        schedules.Any(s => s.IsNightShift) ? 1 : 2;

                    if (punches.Count <= threshold)
                        continue;

                    foreach (var extraPunch in punches.Skip(threshold))
                    {
                        if (extraPunch.PunchId.HasValue)
                            extraPunchIds.Add(extraPunch.PunchId.Value);
                    }
                }
            }

            var filtered = new Dictionary<DateTime, List<CalendarEntry>>();

            foreach (var dayEntry in calendarEntries)
            {
                var employeeIdsWithExtraPunches = dayEntry.Value
                    .Where(e => e.IsPunch && e.PunchId.HasValue && extraPunchIds.Contains(e.PunchId.Value))
                    .Where(e => e.EmployeeId.HasValue)
                    .Select(e => e.EmployeeId!.Value)
                    .ToHashSet();

                if (employeeIdsWithExtraPunches.Count == 0)
                    continue;

                var filteredEntries = dayEntry.Value
                    .Where(e =>
                        e.EmployeeId.HasValue &&
                        employeeIdsWithExtraPunches.Contains(e.EmployeeId.Value))
                    .ToList();

                if (filteredEntries.Count > 0)
                    filtered[dayEntry.Key] = filteredEntries;
            }

            return filtered;
        }
        // -----------------------------
        // ✅ PunchLog (filters + default current month)
        // NOTE: This now returns a ViewModel (PunchLogViewModel), not List<Punch>.
        // You must update PunchLog.cshtml to accept PunchLogViewModel.
        // -----------------------------
        public IActionResult PunchLog(DateTime? from, DateTime? to, string? punchType, string? punchCondition, int? employeeId)
        {
            var now = DateTime.Now;
            var defaultFrom = new DateTime(now.Year, now.Month, now.Day);
            var defaultTo = defaultFrom;

            var vm = new PunchLogViewModel
            {
                From = from ?? defaultFrom,
                To = to ?? defaultTo,
                PunchType = string.IsNullOrWhiteSpace(punchType) ? null : punchType,
                PunchCondition = string.IsNullOrWhiteSpace(punchCondition) ? null : punchCondition,
                EmployeeId = employeeId,
                Employees = _repo.GetEmployees().ToList()
            };

            // Use repo data
            var q = _repo.GetAllPunches().AsQueryable();

            // ✅ Inclusive day range: [From 00:00, To+1day 00:00)
            var fromDt = vm.From.Date;
            var toExclusive = vm.To.Date.AddDays(1);

            q = q.Where(p => p.Timestamp >= fromDt && p.Timestamp < toExclusive);

            if (vm.EmployeeId.HasValue)
                q = q.Where(p => p.EmployeeId == vm.EmployeeId.Value);

            if (!string.IsNullOrWhiteSpace(vm.PunchType))
                q = q.Where(p => p.PunchType.Equals(vm.PunchType, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(vm.PunchCondition))
            {
                q = vm.PunchCondition switch
                {
                    "auto" => q.Where(p => !p.IsManualCreated && !p.IsManualEdited && !p.IsAutoPunchOut),
                    "autoB" => q.Where(p => p.IsAutoPunchOut),
                    "edited" => q.Where(p => p.IsManualEdited),
                    "added" => q.Where(p => p.IsManualCreated),
                    _ => q
                };
            }

            vm.Punches = q
                .OrderBy(p => p.Timestamp)
                .ToList();

            ViewBag.HidePunchImages = _repo.GetAdministrationSettings().HidePunchImages;

            return View(vm);
        }

        // ✅ ADD punch from PunchLog
        [HttpPost]
        public IActionResult AddPunchFromPunchLog(
            int employeeId,
            DateTime punchDate,
            string punchTime,
            string punchType,
            string? note,
            DateTime from,
            DateTime to,
            string? filterPunchType,
            string? filterPunchCondition,
            int? filterEmployeeId)
        {
            if (!TimeSpan.TryParse(punchTime, out var time))
            {
                TempData["Error"] = "Invalid time format";
                return RedirectToAction("PunchLog");
            }

            var timestamp = punchDate.Date + time;

            var punch = new Punch
            {
                EmployeeId = employeeId,
                PunchType = punchType,
                Timestamp = timestamp,
                CreatedAt = DateTime.Now,
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
            };

            // ✅ Manual flag fields (only if you added them to Punch model)
            punch.IsManualCreated = true;
            punch.IsManualEdited = false;
            punch.UpdatedAt = null;

            _repo.AddPunch(punch);

            TempData["Success"] = "Punch added successfully";

            return RedirectToAction("PunchLog", new
            {
                from,
                to,
                punchType = filterPunchType,
                punchCondition = filterPunchCondition,
                employeeId = filterEmployeeId
            });
        }

        // ✅ EDIT punch from PunchLog
        [HttpPost]
        public IActionResult EditPunchFromPunchLog(
            int punchId,
            DateTime punchDate,
            string punchTime,
            string punchType,
            string? note,
            DateTime from,
            DateTime to,
            string? filterPunchType,
            string? filterPunchCondition,
            int? filterEmployeeId)
        {
            var punch = _repo.GetPunches().FirstOrDefault(p => p.Id == punchId);
            if (punch == null)
            {
                TempData["Error"] = "Punch not found";
                return RedirectToAction("PunchLog");
            }

            if (!TimeSpan.TryParse(punchTime, out var time))
            {
                TempData["Error"] = "Invalid time format";
                return RedirectToAction("PunchLog");
            }

            punch.Timestamp = punchDate.Date + time;
            punch.PunchType = punchType;
            punch.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

            // ✅ Manual flag fields (only if you added them to Punch model)
            punch.IsManualEdited = true;
            punch.UpdatedAt = DateTime.Now;

            _repo.UpdatePunch(punch);

            TempData["Success"] = "Punch updated successfully";

            return RedirectToAction("PunchLog", new
            {
                from,
                to,
                punchType = filterPunchType,
                punchCondition = filterPunchCondition,
                employeeId = filterEmployeeId
            });
        }

        // ✅ DELETE punch from PunchLog
        [HttpPost]
        public IActionResult DeletePunchFromPunchLog(
            int punchId,
            DateTime from,
            DateTime to,
            string? filterPunchType,
            string? filterPunchCondition,
            int? filterEmployeeId)
        {
            var punch = _repo.GetPunches().FirstOrDefault(p => p.Id == punchId);
            if (punch == null)
            {
                TempData["Error"] = "Punch not found";
                return RedirectToAction("PunchLog");
            }

            DeletePunchImageFile(punch);
            _repo.DeletePunch(punchId);

            TempData["Success"] = "Punch deleted successfully";

            return RedirectToAction("PunchLog", new
            {
                from,
                to,
                punchType = filterPunchType,
                punchCondition = filterPunchCondition,
                employeeId = filterEmployeeId
            });
        }

        // -----------------------------
        // Calendar (unchanged)
        // -----------------------------
        public IActionResult Calendar(
            int? employeeId,
            int? organisationId,
            int? departmentId,
            bool showExtraPunchesOnly = false,
            bool showMissing = false,
            bool showPunchTypeSuggestions = false,
            DateTime? missingFrom = null,
            DateTime? missingTo = null,
            bool includeMissingIn = false,
            bool includeMissingOut = false,
            int month = 0,
            int year = 0)
        {
            var now = DateTime.Now;
            if (month == 0) month = now.Month;
            if (year == 0) year = now.Year;
            if (year >= 10000) year = 9999;

            var calendarEntries =
                _repo.GetCalendarEntriesForMonth(
                    year, month, employeeId, organisationId, departmentId);
            var administrationSettings = _repo.GetAdministrationSettings();
            ViewBag.CalendarPunchGraceMinutes = administrationSettings.CalendarPunchGraceMinutes > 0
                ? administrationSettings.CalendarPunchGraceMinutes
                : 15;

            var displayedEmployeeIds = calendarEntries.Values
                .SelectMany(entries => entries)
                .Where(entry => entry.EmployeeId.HasValue)
                .Select(entry => entry.EmployeeId!.Value)
                .Distinct()
                .ToList();

            if (showExtraPunchesOnly)
                calendarEntries = FilterToExtraPunches(calendarEntries);

            var groupedCalendarEntries = calendarEntries.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value
                    .GroupBy(e => e.EmployeeName)
                    .Select(g => new CalendarEmployeeGroup
                    {
                        EmployeeName = g.Key,
                        EmployeeId = g.First().EmployeeId,
                        Punches = g.Where(e => e.IsPunch)
                            .OrderBy(e => e.PunchTime)
                            .ToList(),
                        Schedules = g.Where(e => !e.IsPunch && !e.IsVacation)
                            .OrderBy(e => e.ScheduleStart)
                            .ThenBy(e => e.ScheduleEnd)
                            .ToList(),
                        Vacations = g.Where(e => e.IsVacation)
                            .OrderBy(e => e.VacationStartDate)
                            .ThenBy(e => e.VacationEndDate)
                            .ToList()
                    })
                    .ToList());

            var hidePunchImages = administrationSettings.HidePunchImages;
            var hideProfilePictures = administrationSettings.HideProfilePictures;

            var viewModel = new CalendarViewModel
            {
                Year = year,
                Month = month,
                SelectedEmployeeId = employeeId,
                SelectedOrganisationId = organisationId,
                SelectedDepartmentId = departmentId,
                Employees = hideProfilePictures
                    ? _repo.GetEmployees().ToList()
                    : GetEmployeesWithEffectivePhoto(displayedEmployeeIds),
                Organisations = _repo.GetOrganisations().ToList(),
                Departments = organisationId.HasValue
                    ? _repo.GetDepartments()
                        .Where(d => d.OrganisationId == organisationId.Value)
                        .ToList()
                    : _repo.GetDepartments().ToList(),
                Holidays = _repo.GetHolidaysInRange(
                        new DateTime(year, month, 1),
                        new DateTime(year, month, DateTime.DaysInMonth(year, month)))
                    .OrderBy(h => h.Date)
                    .ToList(),
                VacationTypes = _repo.GetVacationTypes()
                    .Where(v => v.IsActive)
                    .OrderBy(v => v.Name)
                    .ToList(),
                CalendarEntries = calendarEntries,
                GroupedCalendarEntries = groupedCalendarEntries
            };

            ViewBag.ShowExtraPunchesOnly = showExtraPunchesOnly;
            ViewBag.HidePunchImages = hidePunchImages;
            ViewBag.HideProfilePictures = hideProfilePictures;

            // -----------------------------
            // Missing punches preview (schedule-based) - shown on Calendar view
            // -----------------------------
            if (showMissing || showPunchTypeSuggestions)
            {
                var periodFrom = (missingFrom ?? new DateTime(year, month, 1)).Date;
                var periodTo = (missingTo ?? new DateTime(year, month, DateTime.DaysInMonth(year, month))).Date;

                // Use current filters to decide which employees to scan
                var emps = _repo.GetEmployees().AsEnumerable();

                if (organisationId.HasValue)
                    emps = emps.Where(e => e.OrganisationId == organisationId.Value);

                if (departmentId.HasValue)
                    emps = emps.Where(e => e.DepartmentId == departmentId.Value);

                if (employeeId.HasValue)
                    emps = emps.Where(e => e.Id == employeeId.Value);

                var selectedEmployees = emps.ToList();

                ViewBag.ShowMissing = showMissing;
                ViewBag.ShowPunchTypeSuggestions = showPunchTypeSuggestions;
                ViewBag.MissingFrom = periodFrom;
                ViewBag.MissingTo = periodTo;
                ViewBag.IncludeMissingIn = includeMissingIn;
                ViewBag.IncludeMissingOut = includeMissingOut;

                if (showMissing)
                {
                    var suggestions = BuildMissingPunchSuggestions(
                        selectedEmployees,
                        periodFrom,
                        periodTo,
                        includeMissingIn,
                        includeMissingOut);

                    ViewBag.MissingPunchSuggestions = suggestions;
                }

                if (showPunchTypeSuggestions)
                {
                    ViewBag.PunchTypeSuggestions = BuildPunchTypeChangeSuggestions(
                        selectedEmployees,
                        periodFrom,
                        periodTo);
                }
            }
            else
            {
                ViewBag.IncludeMissingIn = true;
                ViewBag.IncludeMissingOut = true;
                ViewBag.ShowPunchTypeSuggestions = false;
            }

            return View(viewModel);
        }

        // APPLY: add selected missing punches (from Calendar page)
        [HttpPost]
        public IActionResult AddMissingPunches(
            string[] selectedKeys,
            int? employeeId,
            int? organisationId,
            int? departmentId,
            bool showExtraPunchesOnly,
            DateTime missingFrom,
            DateTime missingTo,
            bool includeMissingIn,
            bool includeMissingOut,
            int month,
            int year)
        {
            if (selectedKeys == null || selectedKeys.Length == 0)
            {
                TempData["Error"] = "No rows selected.";
                return RedirectToAction("Calendar", new
                {
                    employeeId,
                    organisationId,
                    departmentId,
                    showExtraPunchesOnly,
                    month,
                    year,
                    showMissing = true,
                    missingFrom,
                    missingTo,
                    includeMissingIn,
                    includeMissingOut
                });
            }

            // Small duplicate guard: don't insert same type for same employee within ±5 minutes
            var dupTol = TimeSpan.FromMinutes(5);

            var allPunches = _repo.GetPunches().ToList();

            int inserted = 0;
            int skipped = 0;

            foreach (var key in selectedKeys)
            {
                if (!TryParseMissingSuggestionKey(key, out var empId, out var type, out var ts))
                {
                    skipped++;
                    continue;
                }

                // Duplicate check
                var exists = allPunches.Any(p =>
                    p.EmployeeId == empId &&
                    NormType(p.PunchType).Equals(type, StringComparison.OrdinalIgnoreCase) &&
                    Math.Abs((p.Timestamp - ts).TotalMinutes) <= dupTol.TotalMinutes);

                if (exists)
                {
                    skipped++;
                    continue;
                }

                var punch = new Punch
                {
                    EmployeeId = empId,
                    PunchType = type,
                    Timestamp = ts,
                    CreatedAt = DateTime.Now,
                    IsManualCreated = true,
                    IsManualEdited = false,
                    UpdatedAt = null
                };

                _repo.AddPunch(punch);
                inserted++;
            }

            TempData["Success"] = $"Inserted {inserted} missing punches. Skipped {skipped}.";
            return RedirectToAction("Calendar", new
            {
                employeeId,
                organisationId,
                departmentId,
                showExtraPunchesOnly,
                month,
                year,
                showMissing = true,
                missingFrom,
                missingTo,
                includeMissingIn,
                includeMissingOut
            });
        }

        [HttpPost]
        public IActionResult AddVacationsFromMissingSuggestions(
            string[] selectedKeys,
            int vacationTypeId,
            int? employeeId,
            int? organisationId,
            int? departmentId,
            bool showExtraPunchesOnly,
            DateTime missingFrom,
            DateTime missingTo,
            bool includeMissingIn,
            bool includeMissingOut,
            int month,
            int year)
        {
            if (selectedKeys == null || selectedKeys.Length == 0)
            {
                TempData["Error"] = "No rows selected.";
                return RedirectToAction("Calendar", new
                {
                    employeeId,
                    organisationId,
                    departmentId,
                    showExtraPunchesOnly,
                    month,
                    year,
                    showMissing = true,
                    missingFrom,
                    missingTo,
                    includeMissingIn,
                    includeMissingOut
                });
            }

            var vacationType = _repo.GetVacationTypeById(vacationTypeId);
            if (vacationType == null || !vacationType.IsActive)
            {
                TempData["Error"] = "Selected vacation type was not found.";
                return RedirectToAction("Calendar", new
                {
                    employeeId,
                    organisationId,
                    departmentId,
                    showExtraPunchesOnly,
                    month,
                    year,
                    showMissing = true,
                    missingFrom,
                    missingTo,
                    includeMissingIn,
                    includeMissingOut
                });
            }

            var selectedDatesByEmployee = new Dictionary<int, HashSet<DateTime>>();
            var skippedRows = 0;

            foreach (var key in selectedKeys)
            {
                if (!TryParseMissingSuggestionKey(key, out var selectedEmployeeId, out _, out var timestamp))
                {
                    skippedRows++;
                    continue;
                }

                if (!selectedDatesByEmployee.TryGetValue(selectedEmployeeId, out var employeeDates))
                {
                    employeeDates = new HashSet<DateTime>();
                    selectedDatesByEmployee[selectedEmployeeId] = employeeDates;
                }

                employeeDates.Add(timestamp.Date);
            }

            if (selectedDatesByEmployee.Count == 0)
            {
                TempData["Error"] = "No valid selected rows were found.";
                return RedirectToAction("Calendar", new
                {
                    employeeId,
                    organisationId,
                    departmentId,
                    showExtraPunchesOnly,
                    month,
                    year,
                    showMissing = true,
                    missingFrom,
                    missingTo,
                    includeMissingIn,
                    includeMissingOut
                });
            }

            var allSelectedDates = selectedDatesByEmployee.Values.SelectMany(d => d).ToList();
            var selectedEmployeeIds = selectedDatesByEmployee.Keys.ToHashSet();
            var selectedEmployees = _repo.GetEmployees()
                .Where(e => selectedEmployeeIds.Contains(e.Id))
                .ToDictionary(e => e.Id);

            var existingVacationDatesByEmployee = GetVacationDatesByEmployee(
                selectedEmployeeIds,
                allSelectedDates.Min(),
                allSelectedDates.Max());

            var insertedRanges = 0;
            var insertedDays = 0;
            var skippedDays = skippedRows;

            foreach (var pair in selectedDatesByEmployee)
            {
                if (!selectedEmployees.ContainsKey(pair.Key))
                {
                    skippedDays += pair.Value.Count;
                    continue;
                }

                existingVacationDatesByEmployee.TryGetValue(pair.Key, out var existingDates);
                existingDates ??= new HashSet<DateTime>();

                var availableDates = pair.Value
                    .Where(d => !existingDates.Contains(d.Date))
                    .OrderBy(d => d)
                    .ToList();

                skippedDays += pair.Value.Count - availableDates.Count;

                foreach (var range in BuildDateRanges(availableDates))
                {
                    _repo.AddVacation(new Vacation
                    {
                        EmployeeId = pair.Key,
                        VacationTypeId = vacationTypeId,
                        StartDate = range.startDate,
                        EndDate = range.endDate,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });

                    insertedRanges++;
                    insertedDays += (range.endDate.Date - range.startDate.Date).Days + 1;
                }
            }

            TempData["Success"] = $"Added {insertedRanges} vacation range(s) covering {insertedDays} day(s). Skipped {skippedDays} row/day selection(s).";
            return RedirectToAction("Calendar", new
            {
                employeeId,
                organisationId,
                departmentId,
                showExtraPunchesOnly,
                month,
                year,
                showMissing = true,
                missingFrom,
                missingTo,
                includeMissingIn,
                includeMissingOut
            });
        }

        [HttpPost]
        public IActionResult ApplyPunchTypeSuggestions(
            int[] selectedPunchIds,
            int? employeeId,
            int? organisationId,
            int? departmentId,
            bool showExtraPunchesOnly,
            int month,
            int year,
            DateTime missingFrom,
            DateTime missingTo)
        {
            if (selectedPunchIds == null || selectedPunchIds.Length == 0)
            {
                TempData["Error"] = "No punches selected.";
                return RedirectToAction("Calendar", new
                {
                    employeeId,
                    organisationId,
                    departmentId,
                    showExtraPunchesOnly,
                    month,
                    year,
                    showPunchTypeSuggestions = true,
                    missingFrom,
                    missingTo
                });
            }

            var emps = _repo.GetEmployees().AsEnumerable();

            if (organisationId.HasValue)
                emps = emps.Where(e => e.OrganisationId == organisationId.Value);

            if (departmentId.HasValue)
                emps = emps.Where(e => e.DepartmentId == departmentId.Value);

            if (employeeId.HasValue)
                emps = emps.Where(e => e.Id == employeeId.Value);

            var suggestions = BuildPunchTypeChangeSuggestions(
                    emps.ToList(),
                    missingFrom.Date,
                    missingTo.Date)
                .Where(s => selectedPunchIds.Contains(s.PunchId))
                .ToList();

            var punchIds = suggestions.Select(s => s.PunchId).ToHashSet();
            var punches = _repo.GetPunches()
                .Where(p => punchIds.Contains(p.Id))
                .ToDictionary(p => p.Id);

            int updated = 0;
            foreach (var suggestion in suggestions)
            {
                if (!punches.TryGetValue(suggestion.PunchId, out var punch))
                    continue;

                if (!IsAutoPunch(punch))
                    continue;

                if (string.Equals(punch.PunchType ?? string.Empty, suggestion.ProposedType, StringComparison.OrdinalIgnoreCase))
                    continue;

                punch.PunchType = suggestion.ProposedType;
                _repo.UpdatePunch(punch);
                updated++;
            }

            TempData["Success"] = $"Updated PunchType for {updated} punches.";
            return RedirectToAction("Calendar", new
            {
                employeeId,
                organisationId,
                departmentId,
                showExtraPunchesOnly,
                month,
                year,
                showPunchTypeSuggestions = true,
                missingFrom,
                missingTo
            });
        }

        // ADD PUNCH from Calendar
        [HttpPost]
        public IActionResult AddPunchFromCalendar(int employeeId, DateTime punchDate, string punchTime, string punchType, string? note)
        {
            if (!TimeSpan.TryParse(punchTime, out var time))
            {
                TempData["Error"] = "Invalid time format";
                return RedirectToAction("Calendar");
            }

            var timestamp = punchDate.Date + time;

            var punch = new Punch
            {
                EmployeeId = employeeId,
                PunchType = punchType,
                Timestamp = timestamp,
                CreatedAt = DateTime.Now,
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
            };

            // ✅ Manual flag fields (only if you added them to Punch model)
            punch.IsManualCreated = true;
            punch.IsManualEdited = false;
            punch.UpdatedAt = null;

            _repo.AddPunch(punch);

            TempData["Success"] = "Punch added successfully";
            return RedirectToAction("Calendar", new
            {
                employeeId,
                month = punchDate.Month,
                year = punchDate.Year
            });
        }

        [HttpPost]
        public IActionResult AddVacationFromCalendar(int employeeId, int vacationTypeId, DateTime startDate, DateTime endDate, string? notes)
        {
            if (endDate.Date < startDate.Date)
            {
                TempData["Error"] = "Vacation end date must be on or after start date.";
                return RedirectToAction("Calendar", new
                {
                    employeeId,
                    month = startDate.Month,
                    year = startDate.Year
                });
            }

            var employee = _repo.GetEmployees().FirstOrDefault(e => e.Id == employeeId);
            if (employee == null)
            {
                TempData["Error"] = "Employee not found.";
                return RedirectToAction("Calendar");
            }

            var vacationType = _repo.GetVacationTypeById(vacationTypeId);
            if (vacationType == null)
            {
                TempData["Error"] = "Vacation type not found.";
                return RedirectToAction("Calendar", new
                {
                    employeeId,
                    month = startDate.Month,
                    year = startDate.Year
                });
            }

            var overlaps = _repo.GetVacations()
                .Any(v => v.EmployeeId == employeeId &&
                          v.IsActive &&
                          v.StartDate.Date <= endDate.Date &&
                          v.EndDate.Date >= startDate.Date);

            if (overlaps)
            {
                TempData["Error"] = "This employee already has a vacation overlapping this period.";
                return RedirectToAction("Calendar", new
                {
                    employeeId,
                    month = startDate.Month,
                    year = startDate.Year
                });
            }

            _repo.AddVacation(new Vacation
            {
                EmployeeId = employeeId,
                VacationTypeId = vacationTypeId,
                StartDate = startDate.Date,
                EndDate = endDate.Date,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            TempData["Success"] = "Vacation added successfully.";
            return RedirectToAction("Calendar", new
            {
                employeeId,
                month = startDate.Month,
                year = startDate.Year
            });
        }

        // EDIT PUNCH from Calendar
        [HttpPost]
        public IActionResult EditPunchFromCalendar(int punchId, DateTime punchDate, string punchTime, string punchType, string? note)
        {
            var punch = _repo.GetPunches().FirstOrDefault(p => p.Id == punchId);
            if (punch == null)
            {
                TempData["Error"] = "Punch not found";
                return RedirectToAction("Calendar");
            }

            if (!TimeSpan.TryParse(punchTime, out var time))
            {
                TempData["Error"] = "Invalid time format";
                return RedirectToAction("Calendar");
            }

            var timestamp = punchDate.Date + time;

            punch.Timestamp = timestamp;
            punch.PunchType = punchType;
            punch.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

            // ✅ Manual flag fields (only if you added them to Punch model)
            punch.IsManualEdited = true;
            punch.UpdatedAt = DateTime.Now;

            _repo.UpdatePunch(punch);

            TempData["Success"] = "Punch updated successfully";
            return RedirectToAction("Calendar", new
            {
                employeeId = punch.EmployeeId,
                month = punchDate.Month,
                year = punchDate.Year
            });
        }

        // DELETE PUNCH from Calendar
        [HttpPost]
        public IActionResult DeletePunchFromCalendar(int punchId)
        {
            var punch = _repo.GetPunches().FirstOrDefault(p => p.Id == punchId);
            if (punch == null)
            {
                TempData["Error"] = "Punch not found";
                return RedirectToAction("Calendar");
            }

            var employeeId = punch.EmployeeId;
            var punchDate = punch.Timestamp;

            DeletePunchImageFile(punch);
            _repo.DeletePunch(punchId);

            TempData["Success"] = "Punch deleted successfully";
            return RedirectToAction("Calendar", new
            {
                employeeId,
                month = punchDate.Month,
                year = punchDate.Year
            });
        }

        public IActionResult WorkSheet(
    int? organisationId,
    int? departmentId,
    int year = 0,
    int month = 0,
    string? monthValue = null,
    bool embedded = false)
{
    var now = DateTime.Now;

    // Month from <input type="month">
    if (!string.IsNullOrWhiteSpace(monthValue) && monthValue.Length >= 7)
    {
        year = int.Parse(monthValue.Substring(0, 4));
        month = int.Parse(monthValue.Substring(5, 2));
    }

    if (year == 0) year = now.Year;
    if (month == 0) month = now.Month;

    // Load organisations / pick default
    var orgs = _repo.GetOrganisations().ToList();
    var selectedOrg = organisationId.HasValue
        ? orgs.FirstOrDefault(o => o.Id == organisationId.Value)
        : orgs.FirstOrDefault();

    if (selectedOrg != null)
        organisationId = selectedOrg.Id;

    // Departments for selected organisation
    var depts = organisationId.HasValue
        ? _repo.GetDepartments().Where(d => d.OrganisationId == organisationId.Value).ToList()
        : _repo.GetDepartments().ToList();
    var selectedDepartment = departmentId.HasValue
        ? depts.FirstOrDefault(d => d.Id == departmentId.Value)
        : null;

    // Employees
    var employees = _repo.GetEmployees().AsEnumerable();

    if (organisationId.HasValue)
        employees = employees.Where(e => e.OrganisationId == organisationId.Value);

    if (departmentId.HasValue)
        employees = employees.Where(e => e.DepartmentId == departmentId.Value);

    var monthStart = new DateTime(year, month, 1);
    var monthEnd = new DateTime(year, month, DateTime.DaysInMonth(year, month));

    employees = employees.Where(e =>
        (!e.EmploymentStartDate.HasValue || e.EmploymentStartDate.Value.Date <= monthEnd) &&
        (!e.EmploymentEndDate.HasValue || e.EmploymentEndDate.Value.Date >= monthStart));

    var employeesList = employees.ToList();

    // Punches for the month (only for selected employees)
    var empIds = employeesList.Select(e => e.Id).ToHashSet();

    var punches = _repo.GetPunches()
        .Where(p => p.Timestamp.Year == year &&
                    p.Timestamp.Month == month &&
                    empIds.Contains(p.EmployeeId))
        .ToList();

    var schedules = _repo.GetSchedules()
        .Where(s => empIds.Contains(s.EmployeeId) &&
                    s.StartDate.Date <= monthEnd &&
                    s.EndDate.Date >= monthStart)
        .ToList();

    var vacationTypes = _repo.GetVacationTypes()
        .ToDictionary(v => v.Id);

    var vacations = _repo.GetVacationsInRange(monthStart, monthEnd)
        .Where(v => v.IsActive && empIds.Contains(v.EmployeeId))
        .ToList();

    var holidayDates = _repo.GetHolidaysInRange(monthStart, monthEnd)
        .Where(h => h.IsActive)
        .Select(h => h.Date.Date)
        .ToHashSet();

    double RoundHours(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    double GetScheduleNetHours(Schedule schedule)
    {
        var end = schedule.ShiftEnd <= schedule.ShiftStart
            ? schedule.ShiftEnd.Add(TimeSpan.FromDays(1))
            : schedule.ShiftEnd;

        var hours = (end - schedule.ShiftStart).TotalHours - (schedule.BreakMinutes / 60.0);
        return Math.Max(0, hours);
    }

    bool ScheduleAppliesOn(Schedule schedule, DateTime date)
    {
        if (schedule.StartDate.Date > date.Date || schedule.EndDate.Date < date.Date)
            return false;

        return schedule.Days == null ||
               !schedule.Days.Any() ||
               schedule.Days.Contains(date.DayOfWeek);
    }

    double GetActualWorkedHours(List<Punch> dailyPunches)
    {
        if (dailyPunches.Count < 2)
            return 0;

        var ordered = dailyPunches.OrderBy(p => p.Timestamp).ToList();
        return Math.Max(0, (ordered.Last().Timestamp - ordered.First().Timestamp).TotalHours);
    }

    bool IsPaidVacation(VacationType type)
    {
        var name = type.Name ?? string.Empty;
        return name.Contains("ანაზღაურებადი", StringComparison.OrdinalIgnoreCase) &&
               !name.Contains("გარეშე", StringComparison.OrdinalIgnoreCase);
    }

    bool IsUnpaidVacation(VacationType type)
    {
        var name = type.Name ?? string.Empty;
        return name.Contains("ანაზღაურების გარეშე", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("ანაზღაურებისგარეშე", StringComparison.OrdinalIgnoreCase);
    }

    const int MaxDays = 31;
    var daysInMonth = DateTime.DaysInMonth(year, month);

    var model = new WorkSheetViewModel
    {
        Year = year,
        Month = month,
        OrganisationName = selectedOrg?.Name ?? "",
        Department = selectedDepartment?.Name ?? "",
        IdentificationCode = selectedOrg?.OrganisationNumber ?? "",
        FormDate = DateTime.Now,
 
        EmployeeSheets = new List<EmployeeWorkSheet>()
    };

    foreach (var emp in employeesList)
    {
        var empPunches = punches.Where(p => p.EmployeeId == emp.Id).ToList();
        var empSchedules = schedules.Where(s => s.EmployeeId == emp.Id).ToList();
        var empVacations = vacations.Where(v => v.EmployeeId == emp.Id).ToList();

        var entries = new List<WorkSheetDayEntry>();
        double totalHours = 0;

        double workedHoursFirstHalf = 0;
        double workedHoursSecondHalf = 0;
        int workedDaysFirstHalf = 0;
        int workedDaysSecondHalf = 0;

        int weekendDays = 0;
        int absenceDays = 0;
        double holidayWorkedHours = 0;
        double nightHours = 0;
        double sfVacationDays = 0;
        double paidVacationDays = 0;
        double unpaidVacationDays = 0;
        double otherVacationDays = 0;
        var weekHours = new Dictionary<DateTime, double>();

        for (int day = 1; day <= MaxDays; day++)
        {
            if (day > daysInMonth)
            {
                entries.Add(new WorkSheetDayEntry { Day = day, Status = "" });
                continue;
            }

            var date = new DateTime(year, month, day);

            if (date.Date > now.Date)
            {
                entries.Add(new WorkSheetDayEntry { Day = day, Status = "" });
                continue;
            }

            var dailyPunches = empPunches.Where(p => p.Timestamp.Date == date.Date).ToList();
            var vacation = empVacations
                .Where(v => v.StartDate.Date <= date.Date && v.EndDate.Date >= date.Date)
                .OrderBy(v => v.StartDate)
                .FirstOrDefault();

            if (vacation != null && vacationTypes.TryGetValue(vacation.VacationTypeId, out var vacationType))
            {
                var abbreviation = string.IsNullOrWhiteSpace(vacationType.Abbreviation)
                    ? vacationType.Name
                    : vacationType.Abbreviation;

                if (string.Equals(abbreviation, "ს/ფ", StringComparison.OrdinalIgnoreCase))
                    sfVacationDays++;
                else if (IsPaidVacation(vacationType))
                    paidVacationDays++;
                else if (IsUnpaidVacation(vacationType))
                    unpaidVacationDays++;
                else
                    otherVacationDays++;

                entries.Add(new WorkSheetDayEntry
                {
                    Day = day,
                    Status = abbreviation,
                    IsVacation = true,
                    VacationAbbreviation = abbreviation
                });
                continue;
            }

            if (emp.DoesNotNeedSchedule)
            {
                var isWorkingDay = date.DayOfWeek != DayOfWeek.Saturday &&
                    date.DayOfWeek != DayOfWeek.Sunday &&
                    !holidayDates.Contains(date.Date);

                if (!isWorkingDay)
                {
                    entries.Add(new WorkSheetDayEntry
                    {
                        Day = day,
                        Status = "X"
                    });
                    continue;
                }

                var noScheduleWorkedHours = 8;
                var noScheduleStatus = "8";

                if (day <= 15) { workedHoursFirstHalf += noScheduleWorkedHours; workedDaysFirstHalf++; }
                else { workedHoursSecondHalf += noScheduleWorkedHours; workedDaysSecondHalf++; }

                var weekStart = date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
                weekHours[weekStart] = weekHours.TryGetValue(weekStart, out var currentWeekHours)
                    ? currentWeekHours + noScheduleWorkedHours
                    : noScheduleWorkedHours;

                entries.Add(new WorkSheetDayEntry
                {
                    Day = day,
                    Status = noScheduleStatus,
                    WorkedHours = noScheduleWorkedHours,
                    IsWorked = true
                });
                continue;
            }

            var daySchedules = empSchedules.Where(s => ScheduleAppliesOn(s, date)).ToList();
            var scheduledHours = daySchedules.Sum(GetScheduleNetHours);

            var workedHours = scheduledHours > 0
                ? scheduledHours
                : GetActualWorkedHours(dailyPunches);
            workedHours = RoundHours(workedHours);

            string status;

            if (dailyPunches.Any())
            {
                status = "✔";
                status = workedHours > 0 ? workedHours.ToString("0.##") : "0";
                totalHours += workedHours;

                if (day <= 15) { workedHoursFirstHalf += workedHours; workedDaysFirstHalf++; }
                else { workedHoursSecondHalf += workedHours; workedDaysSecondHalf++; }

                if (date.DayOfWeek == DayOfWeek.Saturday ||
                    date.DayOfWeek == DayOfWeek.Sunday ||
                    holidayDates.Contains(date.Date))
                {
                    holidayWorkedHours += workedHours;
                }

                nightHours += daySchedules
                    .Where(s => s.ShiftEnd <= s.ShiftStart)
                    .Sum(GetScheduleNetHours);

                var weekStart = date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
                weekHours[weekStart] = weekHours.TryGetValue(weekStart, out var currentWeekHours)
                    ? currentWeekHours + workedHours
                    : workedHours;
            }
            else if (date.DayOfWeek == DayOfWeek.Saturday ||
                     date.DayOfWeek == DayOfWeek.Sunday ||
                     holidayDates.Contains(date.Date))
            {
                status = "X";
                weekendDays++;
            }
            else
            {
                status = "X";
                absenceDays++;
            }

            entries.Add(new WorkSheetDayEntry
            {
                Day = day,
                Status = status,
                WorkedHours = workedHours,
                IsWorked = dailyPunches.Any()
            });
        }

        var overtimeHours = weekHours.Values.Sum(hours => Math.Max(0, hours - 40));

        model.EmployeeSheets.Add(new EmployeeWorkSheet
        {
            FullName = emp.FullName,        // ✅ no job title here
            TabNumber = emp.PersonalId,
            DailyEntries = entries,
            Position = emp.Position,        // ✅ job title from Employee.Position

            WorkedDaysFirstHalf = workedDaysFirstHalf,
            WorkedHoursFirstHalf = RoundHours(workedHoursFirstHalf),
            WorkedDaysSecondHalf = workedDaysSecondHalf,
            WorkedHoursSecondHalf = RoundHours(workedHoursSecondHalf),

            TotalWorkedHours = RoundHours(workedHoursFirstHalf + workedHoursSecondHalf),
            WeekendDays = weekendDays,
            AbsenceDays = absenceDays,
            AbsenceHours = absenceDays * 8,

            BusinessTripHours = sfVacationDays,
            PaidLeaveHours = paidVacationDays,
            UnpaidLeaveHours = unpaidVacationDays,
            OtherAbsenceHours = otherVacationDays,
            OvertimeHours = RoundHours(overtimeHours),
            NightHours = RoundHours(nightHours),
            WeekendWorkedHours = RoundHours(holidayWorkedHours),
            OtherHours = 0
        });
    }

    // For filters UI
    ViewBag.Organisations = orgs.Select(o => new { o.Id, o.Name });
    ViewBag.Departments = depts.Select(d => new { d.Id, d.Name });
    ViewBag.SelectedOrganisationId = organisationId;
    ViewBag.SelectedDepartmentId = departmentId;
    ViewBag.IsEmbedded = embedded;

    return View(model);
}

    }
}
