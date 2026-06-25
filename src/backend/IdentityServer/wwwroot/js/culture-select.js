(function () {
    const cultureSelect = document.getElementById("culture-select");

    if (!(cultureSelect instanceof HTMLSelectElement) || !cultureSelect.form) {
        return;
    }

    cultureSelect.addEventListener("change", function () {
        cultureSelect.form.submit();
    });
})();
