(function ($) {
    "use strict";

    var section = document.body.dataset.section;
    if (!section) return;
    var base = document.body.dataset.adminBase;
    var records = [], outlets = [], page = 1, pageSize = 10, sortKey = "", ascending = false;
    var currentRequest = 0, editorAction = "", editorKind = "", previewUrl = null;
    var csrf = $("#editorForm input[name='__RequestVerificationToken']").val();
    var ids = { members: "memberID", rewards: "rewardsID", vouchers: "voucherID", highlights: "highlightID", outlets: "outletID", staff: "outletStaffID", points: "pointRecordID", rewardRecords: "rewardRecordID", voucherRecords: "voucherRecordID", feedback: "feedbackID" };
    var names = { members: "member", rewards: "reward", vouchers: "voucher", highlights: "promotion", outlets: "outlet", staff: "staff member", points: "point record", rewardRecords: "reward redemption", voucherRecords: "voucher redemption", feedback: "feedback" };
    var titles = { members: "All members", rewards: "Reward catalog", vouchers: "Voucher catalog", highlights: "Promotional highlights", outlets: "Our outlets", staff: "Outlet staff directory", points: "Points ledger", rewardRecords: "Reward redemptions", voucherRecords: "Voucher redemptions", feedback: "Member feedback" };
    var columns = {
        members: [["memberID", "ID"], ["name", "Member"], ["phoneNumber", "Phone"], ["tier", "Tier"], ["point", "Points"], ["accountStatus", "Status"]],
        rewards: [["rewardsID", "ID"], ["name", "Reward"], ["point", "Points"], ["expireDate", "Expires"], ["status", "Status"]],
        vouchers: [["voucherID", "ID"], ["voucherName", "Voucher"], ["createdDate", "Starts"], ["expiredDate", "Expires"], ["voucherStatus", "Status"]],
        highlights: [["highlightID", "ID"], ["title", "Promotion"], ["createDate", "Starts"], ["expireDate", "Expires"], ["status", "Status"]],
        outlets: [["outletID", "ID"], ["outletName", "Outlet"], ["createdDate", "Created"], ["outletStatus", "Status"]],
        staff: [["outletStaffID", "ID"], ["outletStaffName", "Staff member"], ["outletID", "Outlet"], ["role", "Role"]],
        points: [["pointRecordID", "ID"], ["dateAssign", "Date"], ["member.name", "Member"], ["point", "Points"], ["outletStaff.outletID", "Outlet"], ["pointRecordStatus", "Status"]],
        rewardRecords: [["rewardRecordID", "ID"], ["rewardName", "Reward"], ["memberName", "Member"], ["outletName", "Outlet"], ["memberRedeemDateTime", "Redeemed"]],
        voucherRecords: [["voucherRecordID", "ID"], ["voucherName", "Voucher"], ["memberName", "Member"], ["outletName", "Outlet"], ["memberRedeemDateTime", "Redeemed"]],
        feedback: [["feedbackID", "ID"], ["title", "Feedback"], ["rating", "Rating"], ["category", "Category"], ["memberID", "Member ID"], ["location", "Location"]]
    };

    function kind() { return section === "redemptions" ? $("#typeFilter").val() : section; }
    function get(obj, path) { return path.split(".").reduce(function (v, k) { return v == null ? null : v[k]; }, obj); }
    function message(target, text, success) { $(target).text(text).toggleClass("success", !!success).prop("hidden", !text); }
    function errorText(error) {
        var data = error.responseJSON;
        return data?.errors ? Object.values(data.errors).flat().join(" ") : data?.message || (error.status === 400 ? "The request could not be accepted. Refresh the page and try again." : "Unable to connect. Please try again.");
    }
    function request(action, data, method, multipart) {
        return new Promise(function (resolve, reject) {
            $.ajax({ url: base + action, type: method || "GET", data: data,
                processData: !multipart, contentType: multipart ? false : "application/x-www-form-urlencoded; charset=UTF-8",
                headers: method === "POST" ? { RequestVerificationToken: csrf } : {},
                success: resolve,
                error: function (xhr) {
                    if (xhr.status === 401) window.location.href = base + "Login";
                    reject(xhr);
                }
            });
        });
    }
    function date(value) {
        if (!value) return "—";
        var parsed = new Date(typeof value === "number" || /^\d{13}$/.test(value) ? Number(value) : value);
        return isNaN(parsed) ? String(value) : parsed.toLocaleDateString("en-MY", { day: "numeric", month: "short", year: "numeric" });
    }
    function status(record, key) {
        var value = get(record, key);
        var enums = { accountStatus: ["Pending", "Active", "Inactive"], outletStatus: ["Pending", "Active", "Deactive"], pointRecordStatus: ["Pending", "Approve", "Rejected"] };
        if (value != null && enums[key] && /^\d+$/.test(String(value))) return enums[key][Number(value)] || String(value);
        return value || "—";
    }
    function isDate(key) { return /date|time/i.test(key) && !/createdBy/i.test(key); }
    function outletName(id) { return outlets.find(function (o) { return Number(o.outletID) === Number(id); })?.outletName || (id ? "Outlet #" + id : "—"); }
    function textValue(record, key) {
        if (/status$/i.test(key)) return status(record, key);
        var value = get(record, key);
        if (/outletID$/.test(key)) return outletName(value);
        return isDate(key) ? date(value) : value == null || value === "" ? "—" : String(value);
    }
    function image(record, css) {
        return customerImages.create(record.image, kind() === "highlights" ? "MerchantHighlight" : "MerchantReward", record.title || record.name || record.voucherName, css, "○");
    }
    function dataQuery() {
        var data = { section: kind() };
        if ((section === "points" || section === "staff") && $("#outletFilter").val()) data.outletID = $("#outletFilter").val();
        if (section === "feedback" && $("#memberFilter").val()) data.memberID = $("#memberFilter").val();
        return data;
    }
    function fillStatuses() {
        var key = columns[kind()].find(function (c) { return /status$/i.test(c[0]); })?.[0];
        $("#statusFilterWrap").prop("hidden", !key);
        var previous = $("#statusFilter").val();
        $("#statusFilter").empty().append($("<option>").val("").text("All statuses"));
        if (key) [...new Set(records.map(function (r) { return status(r, key); }))].sort().forEach(function (s) { $("#statusFilter").append($("<option>").val(s).text(s)); });
        $("#statusFilter").val(previous && $("#statusFilter option").toArray().some(function (o) { return o.value === previous; }) ? previous : "");
    }
    async function load() {
        var generation = ++currentRequest;
        $("#loading").prop("hidden", false);
        $("#refreshButton").prop("disabled", true);
        $("#recordsTable").attr("aria-busy", "true");
        message("#pageMessage", "");
        try {
            if (section === "dashboard") await loadDashboard(generation);
            else {
                if (section === "staff" && !$("#outletFilter").val()) {
                    records = []; render(); return;
                }
                var result = await request("Data", dataQuery());
                if (generation !== currentRequest) return;
                records = Array.isArray(result) ? result : [];
                page = 1; fillStatuses(); render();
            }
        } catch (error) {
            if (generation !== currentRequest) return;
            // The feedback API uses 400 instead of an empty list for a member with no feedback.
            if (section === "feedback" && /No Records Found/i.test(errorText(error))) {
                records = []; page = 1; render();
            } else {
                records = []; render();
                message("#pageMessage", errorText(error));
                $("#emptyTitle").text("Records couldn’t be loaded");
                $("#emptyDescription").text("Use Refresh to try again.");
            }
        } finally {
            if (generation === currentRequest) {
                $("#loading").prop("hidden", true);
                $("#refreshButton").prop("disabled", false);
                $("#recordsTable").attr("aria-busy", "false");
            }
        }
    }

    async function loadDashboard(generation) {
        var sources = ["members", "rewards", "vouchers", "points", "highlights", "outlets"];
        var results = await Promise.allSettled(sources.map(function (s) { return request("Data", { section: s }); }));
        if (generation !== currentRequest) return;
        var datasets = {}, failures = [];
        results.forEach(function (r, i) { datasets[sources[i]] = r.status === "fulfilled" && Array.isArray(r.value) ? r.value : null; if (r.status === "rejected") failures.push(sources[i]); });
        $("#dashboardContent").prop("hidden", false);
        $("#metrics").empty();
        [["members", "Total members", "Members in your community"], ["rewards", "Active rewards", "Available in the reward catalog"], ["vouchers", "Active vouchers", "Offers for your members"], ["points", "Point records", "All recorded point transactions"]].forEach(function (metric) {
            var items = datasets[metric[0]];
            var count = items ? items.filter(function (r) { return metric[0] === "rewards" ? status(r, "status") === "Active" : metric[0] === "vouchers" ? status(r, "voucherStatus") === "Active" : true; }).length : "—";
            var card = $("<a>").addClass("metric").attr("href", base + metric[0][0].toUpperCase() + metric[0].slice(1));
            card.append($("<span>").addClass("metric-top").text(metric[1]).append($("<span>").text("↗")), $("<strong>").addClass("metric-value").text(typeof count === "number" ? count.toLocaleString() : count), $("<span>").addClass("metric-note").text(items ? metric[2] : "Temporarily unavailable"));
            $("#metrics").append(card);
        });
        $("#pointsChart").empty();
        if (datasets.points) {
            var counts = ["Approve", "Pending", "Rejected"].map(function (s) { return datasets.points.filter(function (p) { return status(p, "pointRecordStatus") === s; }).length; });
            var max = Math.max.apply(null, counts.concat([1]));
            ["Approved", "Pending", "Rejected"].forEach(function (label, i) {
                var track = $("<div>").addClass("chart-track").append($("<div>").addClass("chart-bar").css("width", (counts[i] / max * 100) + "%"));
                $("#pointsChart").append($("<div>").addClass("chart-row").append($("<span>").text(label), track, $("<strong>").text(counts[i].toLocaleString())));
            });
            $("#pointsChart").append($("<p>").addClass("chart-note").text(datasets.points.length ? "Number of point transactions by review status." : "No point transactions have been recorded yet."));
        } else $("#pointsChart").text("Point activity is temporarily unavailable.");
        records = [];
        sources.filter(function (s) { return s !== "points"; }).forEach(function (s) { (datasets[s] || []).forEach(function (r) { records.push({ _section: s, _id: r[ids[s]], name: r.name || r.voucherName || r.title || r.outletName || "Record #" + r[ids[s]], email: r.email || "", type: names[s], _status: status(r, s === "members" ? "accountStatus" : s === "outlets" ? "outletStatus" : s === "vouchers" ? "voucherStatus" : "status") }); }); });
        page = 1; render();
        if (failures.length) message("#pageMessage", "Some sections are unavailable: " + failures.join(", ") + ". Refresh to try again.");
    }

    function render() {
        var k = kind(), dashboard = section === "dashboard";
        var cols = dashboard ? [["name", "Name"], ["type", "Type"], ["email", "Email"], ["_status", "Status"]] : columns[k];
        $("#tableTitle").text(dashboard ? "Community directory" : titles[k]);
        $("#tableEyebrow").text(dashboard ? "SEARCH YOUR WORKSPACE" : "WORKSPACE DIRECTORY");
        var query = $("#searchInput").val().trim().toLowerCase();
        var selectedStatus = $("#statusFilter").val();
        var statusKey = cols.find(function (c) { return /status$/i.test(c[0]); })?.[0];
        var filtered = records.filter(function (r) {
            var searchable = dashboard ? [r.name, r.email, r.type, r._id].join(" ") : cols.map(function (c) { return textValue(r, c[0]); }).join(" ") + " " + (r.email || "") + " " + (r.memberPhoneNumber || "");
            return searchable.toLowerCase().includes(query) && (!selectedStatus || !statusKey || status(r, statusKey) === selectedStatus);
        });
        var key = sortKey || (dashboard ? "name" : ids[k]);
        filtered.sort(function (a, b) {
            var x = get(a, key), y = get(b, key);
            var result = typeof x === "number" && typeof y === "number" ? x - y : String(x ?? "").localeCompare(String(y ?? ""), undefined, { numeric: true });
            return ascending ? result : -result;
        });
        var totalPages = Math.max(1, Math.ceil(filtered.length / pageSize));
        page = Math.min(page, totalPages);
        var head = $("<tr>");
        cols.forEach(function (c) {
            var th = $("<th>").attr("scope", "col");
            if (key === c[0]) th.attr("aria-sort", ascending ? "ascending" : "descending");
            th.append($("<button>").attr("type", "button").text(c[1] + (key === c[0] ? ascending ? " ↑" : " ↓" : "")).on("click", function () { ascending = key === c[0] ? !ascending : true; sortKey = c[0]; render(); }));
            head.append(th);
        });
        head.append($("<th>").attr("scope", "col").text("Actions"));
        $("#recordsTable thead").empty().append(head);
        var body = $("#recordsTable tbody").empty();
        filtered.slice((page - 1) * pageSize, page * pageSize).forEach(function (record) {
            var row = $("<tr>");
            cols.forEach(function (c) {
                var cell = $("<td>");
                if (dashboard) cell.text(record[c[0]] || "—");
                else if (/status$/i.test(c[0])) { var s = status(record, c[0]); cell.append($("<span>").addClass("badge " + s.toLowerCase().replace(/[^a-z]/g, "")).text(s)); }
                else if (["rewards", "vouchers", "highlights"].includes(k) && c[0] === cols[1][0]) cell.append($("<div>").addClass("record-cell").append(image(record, "record-image"), $("<div>").append($("<strong>").text(textValue(record, c[0])), $("<small>").text(record.shortTitle || ""))));
                else if (k === "members" && c[0] === "name") cell.append($("<strong>").text(record.name || "—"), $("<small>").text(record.email || ""));
                else cell.text(textValue(record, c[0]));
                row.append(cell);
            });
            var actions = $("<div>").addClass("table-actions");
            function action(label, fn, css) { actions.append($("<button>").attr("type", "button").addClass(css || "").text(label).on("click", fn)); }
            action("View", function () { showDetails(dashboard ? record._section : k, dashboard ? record._id : record[ids[k]]); });
            if (!dashboard) {
                if (["members", "rewards", "vouchers", "highlights", "staff"].includes(k)) action("Edit", function () { editRecord(k, record[ids[k]]); });
                if (["members", "outlets"].includes(k)) {
                    var active = status(record, k === "members" ? "accountStatus" : "outletStatus") === "Active";
                    action(active ? "Deactivate" : "Activate", function () { confirmChange((active ? "Deactivate " : "Activate ") + names[k] + "?", "Change the status of “" + (record.name || record.outletName || record[ids[k]]) + "”?", "ToggleStatus", { kind: k, id: record[ids[k]] }); });
                }
                if (k === "outlets") actions.append($("<a>").attr("href", base + "Staff?outletID=" + record.outletID).text("Staff"));
                if (["rewards", "vouchers", "highlights", "staff"].includes(k)) action("Delete", function () { confirmChange("Delete " + names[k] + "?", "Permanently delete “" + (record.name || record.voucherName || record.title || record.outletStaffName) + "”? This cannot be undone.", "Delete", { kind: k, id: record[ids[k]] }); }, "delete-action");
                if (k === "points" && status(record, "pointRecordStatus") !== "Rejected") action("Reject", function () { openReject(record); }, "delete-action");
            }
            row.append($("<td>").append(actions)); body.append(row);
        });
        $("#recordCount").text(filtered.length.toLocaleString() + " records");
        $("#emptyState").prop("hidden", filtered.length > 0);
        $("#emptyTitle").text(query || selectedStatus ? "No matching records" : section === "staff" && !$("#outletFilter").val() ? "Choose an outlet" : "No records yet");
        $("#emptyDescription").text(query || selectedStatus ? "Try a different search or clear the filters." : "Records will appear here when they’re available.");
        $("#pageInfo").text(filtered.length ? ((page - 1) * pageSize + 1) + "–" + Math.min(page * pageSize, filtered.length) + " of " + filtered.length + " records" : "0 records");
        $("#previousPage").prop("disabled", page === 1); $("#nextPage").prop("disabled", page === totalPages);
    }

    function labelFor(key) { return key.replace(/([a-z])([A-Z])/g, "$1 $2").replace(/\bID\b/g, "ID").replace(/^./, function (c) { return c.toUpperCase(); }); }
    async function showDetails(k, id) {
        try {
            $("#loading").prop("hidden", false);
            var record = await request("Detail", { section: k, id: id });
            if (!record) throw { responseJSON: { message: "Record not found." } };
            $("#detailsTitle").text(record.title || record.name || record.outletName || record.outletStaffName || labelFor(names[k]) + " #" + id);
            var content = $("#detailsContent").empty();
            if (record.image && ["rewards", "vouchers", "highlights"].includes(k)) content.append(customerImages.create(record.image, k === "highlights" ? "MerchantHighlight" : "MerchantReward", record.title || "", "detail-image", "○"));
            function details(obj) {
                var list = $("<dl>").addClass("detail-list");
                Object.entries(obj).forEach(function (entry) {
                    var key = entry[0], value = entry[1];
                    if (value == null || value === "" || ["image", "pin", "$id", "$ref"].includes(key)) return;
                    var block = $("<div>").append($("<dt>").text(labelFor(key)));
                    var dd = $("<dd>");
                    if (Array.isArray(value)) { block.addClass("detail-nested"); if (!value.length) dd.text("No restrictions"); else value.forEach(function (v) { dd.append(typeof v === "object" ? details(v) : $("<p>").text(String(v))); }); }
                    else if (typeof value === "object") { block.addClass("detail-nested"); dd.append(details(value)); }
                    else dd.text(/status$/i.test(key) ? status(obj, key) : isDate(key) ? date(value) : typeof value === "boolean" ? value ? "Yes" : "No" : String(value));
                    list.append(block.append(dd));
                });
                return list;
            }
            content.append(details(record));
            if (k === "outlets") content.append($("<a>").addClass("button secondary").attr("href", base + "Staff?outletID=" + id).text("View outlet staff →"));
            document.getElementById("detailsDialog").showModal();
        } catch (error) { message("#pageMessage", errorText(error)); }
        finally { $("#loading").prop("hidden", true); }
    }

    function field(name, label, options) {
        options = options || {};
        var wrapper = $("<div>").addClass("form-field" + (options.wide ? " wide" : ""));
        var id = "field" + name;
        var control = options.type === "textarea" ? $("<textarea>") : options.type === "select" ? $("<select>") : $("<input>").attr("type", options.type || "text");
        control.attr({ name: name, id: id });
        if (options.required) control.prop("required", true);
        ["min", "max", "minlength", "maxlength", "pattern", "step", "accept", "autocomplete"].forEach(function (key) { if (options[key] !== undefined) control.attr(key, options[key]); });
        if (options.type === "select") (options.choices || []).forEach(function (choice) { control.append($("<option>").val(choice[0]).text(choice[1])); });
        if (options.value != null) control.val(options.value);
        if (options.type === "password") control.attr("autocomplete", "new-password");
        wrapper.append($("<label>").attr("for", id).text(label + (options.required ? " *" : "")), control);
        if (options.hint) wrapper.append($("<p>").addClass("field-hint").text(options.hint));
        wrapper.append($("<span>").addClass("field-error").attr({ id: id + "Error", "aria-live": "polite" }));
        control.attr("aria-describedby", id + "Error");
        $("#editorFields").append(wrapper);
        return control;
    }
    function hidden(name, value) { $("#editorFields").append($("<input>").attr({ type: "hidden", name: name }).val(value)); }
    function dateInput(value, end) {
        if (value) return String(value).slice(0, 16);
        var d = new Date(); d.setDate(d.getDate() + (end ? 30 : 0));
        return new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
    }
    function outletChoices() { return [["", "Choose an outlet"]].concat(outlets.map(function (o) { return [o.outletID, o.outletName]; })); }
    function setupEditor(title, action, k) {
        editorAction = action; editorKind = k;
        $("#editorTitle").text(title); $("#editorFields").empty(); message("#editorMessage", "");
        $("#saveButton").prop("disabled", false).text("Save changes");
    }
    async function editRecord(k, id) {
        try {
            var record = id ? await request("Detail", { section: k, id: id }) : {};
            if (!record) throw { responseJSON: { message: "Record not found." } };
            setupEditor((id ? "Edit " : "Create ") + names[k], k === "members" ? "SaveMember" : k === "staff" ? "SaveStaff" : k === "outlets" ? "SaveOutlet" : "SaveCatalog", k);
            hidden("Id", id || 0);
            if (k === "members") {
                field("Name", "Full name", { required: true, maxlength: 150, value: record.name });
                field("Email", "Email address", { type: "email", required: true, maxlength: 254, value: record.email });
                field("PhoneNumber", "Phone number", { type: "tel", required: true, pattern: "0[0-9]{8,10}", value: (record.phoneNumber || "").replace(/^\+60/, "0"), hint: "Use a local number starting with 0." });
                field("BirthDate", "Birth date", { type: "date", required: true, max: new Date().toLocaleDateString("en-CA"), value: (record.birthDate || "").slice(0, 10) });
                field("Tier", "Membership tier", { maxlength: 50, value: record.tier || "Member" });
                field("ReferralBy", "Referred by", { maxlength: 100, value: record.referralBy });
                if (!id) field("Password", "Initial password", { type: "password", required: true, minlength: 6, maxlength: 200 });
            } else if (k === "outlets") {
                field("OutletName", "Outlet name", { required: true, maxlength: 150, wide: true });
            } else if (k === "staff") {
                field("OutletStaffName", "Staff name", { required: true, maxlength: 150, value: record.outletStaffName });
                field("OutletID", "Outlet", { type: "select", required: true, choices: outletChoices(), value: record.outletID || $("#outletFilter").val() });
                field("Pin", "Staff PIN", { type: "password", required: true, pattern: "[0-9]{6}", maxlength: 6, value: record.pin, hint: "Exactly six digits." });
                field("Role", "Role", { maxlength: 100, value: record.role });
            } else {
                if (k !== "highlights") field("Name", k === "vouchers" ? "Voucher name" : "Reward name", { required: true, maxlength: 150, value: record.name || record.voucherName });
                field("Title", "Title", { required: true, maxlength: 200, value: record.title });
                field("SubTitle", "Subtitle", { maxlength: 300, value: record.subTitle || record.subtitle });
                if (k !== "highlights") field("ShortTitle", "Short title", { maxlength: 150, value: record.shortTitle });
                if (k === "rewards") field("Point", "Points required", { type: "number", required: true, min: 0, max: 2147483647, step: 1, value: record.point ?? 100 });
                field("StartDate", "Starts", { type: "datetime-local", required: true, value: dateInput(record.createDate || record.createdDate) });
                field("EndDate", "Expires", { type: "datetime-local", required: true, value: dateInput(record.expireDate || record.expiredDate, true) });
                if (k === "rewards") field("Status", "Status", { type: "select", required: true, choices: [["Active", "Active"], ["Deactive", "Deactive"], ["Expired", "Expired"]], value: record.status || "Active", hint: id ? "The API may override this status based on the reward’s expiry date." : "" });
                else { hidden("Status", "Active"); $("#editorFields").append($("<p>").addClass("form-note").text("Availability is determined by the promotion or voucher dates.")); }
                field("Description", "Description", { type: "textarea", wide: true, maxlength: 10000, value: record.description });
                field("TnC", "Terms & conditions", { type: "textarea", wide: true, maxlength: 10000, value: record.tnC || record.tnc });
                var upload = field("File", id ? "Replace image" : "Image", { type: "file", wide: true, required: k === "highlights" && !id, accept: "image/jpeg,image/png,image/webp,image/gif", hint: "JPG, PNG, WebP or GIF, up to 5 MB. Leave empty to keep the current image." });
                var preview = $("<img>").addClass("image-preview").attr("alt", "Image preview").prop("hidden", !record.image);
                if (record.image) preview.attr("src", customerImages.url(record.image, k === "highlights" ? "MerchantHighlight" : "MerchantReward"));
                upload.after(preview);
                upload.on("change", function () {
                    if (previewUrl) URL.revokeObjectURL(previewUrl);
                    var file = this.files[0];
                    if (file && file.size > 5 * 1024 * 1024) { this.setCustomValidity("Choose an image smaller than 5 MB."); this.reportValidity(); return; }
                    this.setCustomValidity("");
                    if (file) { previewUrl = URL.createObjectURL(file); preview.attr("src", previewUrl).prop("hidden", false); }
                    else preview.prop("hidden", true);
                });
                if (k === "vouchers") buildRules(record.voucherRules || []);
            }
            document.getElementById("editorDialog").showModal();
        } catch (error) { message("#pageMessage", errorText(error)); }
    }

    function buildRules(rules) {
        var wrapper = $("<div>").addClass("form-field wide").append($("<label>").text("Allowed redemption days & times"));
        var editor = $("<div>").addClass("rules-editor"), rows = $("<div>").attr("id", "ruleRows");
        var days = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];
        function addRule(rule) {
            if (rows.children().length >= 7) return;
            rule = rule || {};
            var row = $("<div>").addClass("rule-row").data("ruleID", rule.voucherRuleID || 0);
            var day = $("<select>").addClass("rule-day").attr("aria-label", "Redemption day");
            days.forEach(function (d) { day.append($("<option>").text(d).val(d)); });
            day.val(rule.dayOfWeek || days.find(function (d) { return !rows.find(".rule-day").toArray().some(function (s) { return s.value === d; }); }));
            row.append(day, $("<input>").attr({ type: "time", required: true, "aria-label": "Start time" }).addClass("rule-start").val((rule.startTime || "09:00").slice(0, 5)), $("<input>").attr({ type: "time", required: true, "aria-label": "End time" }).addClass("rule-end").val((rule.endTime || "22:00").slice(0, 5)), $("<button>").attr({ type: "button", "aria-label": "Remove redemption day" }).addClass("icon-button").text("×").on("click", function () { row.remove(); }));
            rows.append(row);
        }
        editor.append(rows, $("<button>").addClass("button secondary").attr("type", "button").text("+ Add day").on("click", function () { addRule(); }));
        wrapper.append(editor, $("<p>").addClass("field-hint").text("Leave empty for no day or time restrictions."));
        $("#editorFields").append(wrapper);
        rules.forEach(addRule);
    }
    function openAssign() {
        setupEditor("Assign member points", "AssignPoints", "points");
        field("PhoneNumber", "Member phone number", { type: "tel", required: true, pattern: "0[0-9]{8,10}", hint: "Use a local phone number starting with 0." });
        field("Point", "Points to assign", { type: "number", required: true, min: 1, max: 2147483647, step: 1 });
        field("OutletID", "Outlet", { type: "select", required: true, choices: outletChoices(), value: $("#outletFilter").val() });
        field("Pin", "Outlet staff PIN", { type: "password", required: true, pattern: "[0-9]{6}", maxlength: 6 });
        document.getElementById("editorDialog").showModal();
    }
    function openReject(record) {
        setupEditor("Reject point record #" + record.pointRecordID, "RejectPoint", "points");
        hidden("Id", record.pointRecordID);
        field("OutletID", "Verifying outlet", { type: "select", required: true, choices: outletChoices(), value: record.outletStaff?.outletID });
        field("Pin", "Outlet staff PIN", { type: "password", required: true, pattern: "[0-9]{6}", maxlength: 6 });
        field("Reason", "Reason for rejection", { type: "textarea", required: true, wide: true, maxlength: 1000 });
        $("#editorFields").append($("<p>").addClass("form-note").text("Approved points will be reversed. Your reason is recorded in the portal audit log; the API stores the rejection status."));
        $("#saveButton").text("Reject record");
        document.getElementById("editorDialog").showModal();
    }
    function confirmChange(title, description, action, data) {
        $("#confirmTitle").text(title); $("#confirmDescription").text(description); message("#confirmMessage", "");
        $("#confirmButton").prop("disabled", false).off("click").on("click", async function () {
            var button = $(this).prop("disabled", true);
            try { var response = await request(action, data, "POST"); document.getElementById("confirmDialog").close(); await load(); message("#pageMessage", response.message, true); }
            catch (error) { message("#confirmMessage", errorText(error)); }
            finally { button.prop("disabled", false); }
        });
        document.getElementById("confirmDialog").showModal();
    }

    $("#editorForm").on("submit", async function (event) {
        event.preventDefault();
        var form = this;
        form.querySelectorAll(".invalid").forEach(function (e) { e.classList.remove("invalid"); e.removeAttribute("aria-invalid"); });
        $(".field-error", form).text(""); message("#editorMessage", "");
        var data = new FormData(form);
        if (editorAction === "SaveCatalog") {
            data.set("kind", editorKind);
            if (data.get("StartDate") > data.get("EndDate")) { message("#editorMessage", "End date must be on or after start date."); return; }
            if (editorKind === "vouchers") {
                var rules = $("#ruleRows .rule-row").toArray().map(function (row) { return { voucherRuleID: $(row).data("ruleID"), dayOfWeek: $(row).find(".rule-day").val(), startTime: $(row).find(".rule-start").val() + ":00", endTime: $(row).find(".rule-end").val() + ":00" }; });
                if (new Set(rules.map(function (r) { return r.dayOfWeek; })).size !== rules.length || rules.some(function (r) { return r.startTime >= r.endTime; })) { message("#editorMessage", "Choose unique days and an end time after each start time."); return; }
                data.set("VoucherRules", JSON.stringify(rules));
            }
        }
        // An untouched file control is an empty File object; omit it from the request.
        if (data.get("File") instanceof File && !data.get("File").size) data.delete("File");
        $("#saveButton").prop("disabled", true).text("Saving…");
        try {
            var response = await request(editorAction, data, "POST", true);
            document.getElementById("editorDialog").close();
            await load(); message("#pageMessage", response.message || "Changes saved successfully.", true);
        } catch (error) {
            message("#editorMessage", errorText(error));
            Object.entries(error.responseJSON?.errors || {}).forEach(function (entry) {
                var control = Array.from(form.elements).find(function (el) { return el.name.toLowerCase() === entry[0].replace(/^input\./i, "").toLowerCase(); });
                if (control) { $(control).addClass("invalid").attr("aria-invalid", "true"); $(control).closest(".form-field").find(".field-error").text(entry[1].join(" ")); }
            });
        } finally { $("#saveButton").prop("disabled", false).text(editorAction === "RejectPoint" ? "Reject record" : "Save changes"); }
    });

    $(".close-dialog").on("click", function () { this.closest("dialog").close(); });
    $("#editorDialog").on("close", function () { if (previewUrl) { URL.revokeObjectURL(previewUrl); previewUrl = null; } });
    $("#refreshButton").on("click", load);
    $("#searchInput").on("input", function () { page = 1; render(); });
    $("#statusFilter").on("change", function () { page = 1; render(); });
    $("#outletFilter, #memberFilter, #typeFilter").on("change", function () { sortKey = ""; load(); });
    $("#previousPage").on("click", function () { page--; render(); });
    $("#nextPage").on("click", function () { page++; render(); });
    $("#createButton").on("click", function () { if (section === "points") openAssign(); else editRecord(section); });

    async function initialize() {
        $("#loading").prop("hidden", false);
        var writable = ["members", "rewards", "vouchers", "highlights", "outlets", "staff", "points"].includes(section);
        $("#createButton").prop("hidden", !writable).text(section === "points" ? "+ Assign points" : "+ Create " + names[section]);
        $("#typeFilterWrap").prop("hidden", section !== "redemptions");
        if (["outlets", "staff", "points"].includes(section)) {
            $("#createButton").prop("disabled", section !== "outlets");
            try {
                outlets = await request("Data", { section: "outlets" });
                $("#outletFilterWrap").prop("hidden", section === "outlets");
                outlets.forEach(function (o) { $("#outletFilter").append($("<option>").val(o.outletID).text(o.outletName)); });
                if (section === "staff") {
                    $("#outletFilter option:first").text("Choose an outlet");
                    var requested = new URLSearchParams(window.location.search).get("outletID");
                    $("#outletFilter").val(outlets.some(function (o) { return String(o.outletID) === requested; }) ? requested : outlets[0]?.outletID || "");
                }
                $("#createButton").prop("disabled", section !== "outlets" && !outlets.length);
            } catch (error) {
                $("#loading").prop("hidden", true);
                message("#pageMessage", "Unable to load outlet choices. " + errorText(error));
                $("#refreshButton").off("click").on("click", function () { window.location.reload(); });
                return;
            }
        }
        if (section === "feedback") {
            $("#memberFilterWrap").prop("hidden", false);
            try {
                var members = await request("Data", { section: "members" });
                members.forEach(function (m) { $("#memberFilter").append($("<option>").val(m.memberID).text("#" + m.memberID + " · " + (m.name || m.email || "Member"))); });
            } catch (_) { $("#memberFilterWrap").prop("hidden", true); }
        }
        await load();
    }
    initialize();
})(jQuery);
