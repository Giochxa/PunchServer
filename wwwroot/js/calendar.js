document.addEventListener("DOMContentLoaded", function () {
    const employeeSelect = document.getElementById("employeeSelect");

    if (employeeSelect) {
        employeeSelect.addEventListener("change", function () {
            const employeeId = employeeSelect.value;
            const url = `/Home/Calendar?employeeId=${employeeId}`;
            window.location.href = url;
        });
    }

    const prevBtn = document.getElementById("prevMonthBtn");
    const nextBtn = document.getElementById("nextMonthBtn");
    const year = document.getElementById("year").value;
    const month = document.getElementById("month").value;
    const selectedEmployee = document.getElementById("employeeSelect")?.value || "";

    if (prevBtn) {
        prevBtn.addEventListener("click", () => {
            const url = `/Home/Calendar?year=${year}&month=${month - 1}&employeeId=${selectedEmployee}`;
            window.location.href = url;
        });
    }

    if (nextBtn) {
        nextBtn.addEventListener("click", () => {
            const url = `/Home/Calendar?year=${year}&month=${parseInt(month) + 1}&employeeId=${selectedEmployee}`;
            window.location.href = url;
        });
    }
});
