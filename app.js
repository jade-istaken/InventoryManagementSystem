/*
  Inventory Management System - Frontend Logic
  Matches the C# backend schema:
    User (abstract) -> Admin, Staff
    Product: SKU, Name, Manufacturer, Category, Quantity, ReorderLevel, Price
    Order: OrderID, SKU, UserName, Amount, Cost
    Sale: OrderID, SKU, UserName, Amount, Income
    Adjustment: AdjustmentID, SKU, UserName, OldPrice, NewPrice, Reason
*/

// ============================================================
//  HELPERS
// ============================================================
function makeSKU() {
    var chars = "ABCDEF0123456789";
    var result = "";
    for (var i = 0; i < 12; i++) {
        result += chars.charAt(Math.floor(Math.random() * chars.length));
    }
    return result.substring(0, 4) + "-" + result.substring(4, 8) + "-" + result.substring(8);
}

function escapeHTML(text) {
    var div = document.createElement("div");
    div.textContent = text;
    return div.innerHTML;
}

function timeNow() {
    return new Date().toLocaleTimeString("en-US", { hour: "numeric", minute: "2-digit", second: "2-digit" });
}

function formatMoney(n) {
    return "$" + Number(n).toLocaleString("en", { minimumFractionDigits: 2 });
}


// ============================================================
//  SEED DATA
// ============================================================
var products = [
    { sku: makeSKU(), name: "Cold-Rolled Steel Sheet", manufacturer: "Nippon Steel", category: "RawMaterial", quantity: 1240, reorderLevel: 300, price: 18.75 },
    { sku: makeSKU(), name: "Copper Wire 14AWG", manufacturer: "Southwire", category: "RawMaterial", quantity: 85, reorderLevel: 200, price: 6.30 },
    { sku: makeSKU(), name: "Servo Motor SM-400", manufacturer: "SELF", category: "Finished", quantity: 44, reorderLevel: 20, price: 312.00 },
    { sku: makeSKU(), name: "Silicone Gasket Ring", manufacturer: "Parker Hannifin", category: "RawMaterial", quantity: 6800, reorderLevel: 1500, price: 1.10 },
    { sku: makeSKU(), name: "PCB Assembly Rev.7", manufacturer: "SELF", category: "Finished", quantity: 190, reorderLevel: 50, price: 67.50 },
    { sku: makeSKU(), name: "Hydraulic Cylinder HC-20", manufacturer: "Bosch Rexroth", category: "Finished", quantity: 12, reorderLevel: 15, price: 489.00 },
    { sku: makeSKU(), name: "Aluminum Extrusion 6061", manufacturer: "Alcoa", category: "RawMaterial", quantity: 520, reorderLevel: 150, price: 22.40 },
    { sku: makeSKU(), name: "Power Supply Unit 24V", manufacturer: "SELF", category: "Finished", quantity: 78, reorderLevel: 25, price: 145.00 }
];

var users = [
    { userName: "admin", firstName: "Diana", lastName: "Kovacs", password: "admin123", role: "Admin" },
    { userName: "jwhitfield", firstName: "James", lastName: "Whitfield", password: "admin123", role: "Admin" },
    { userName: "mobi", firstName: "Marcus", lastName: "Obi", password: "staff123", role: "Staff" },
    { userName: "slindgren", firstName: "Sarah", lastName: "Lindgren", password: "staff123", role: "Staff" },
    { userName: "pnair", firstName: "Priya", lastName: "Nair", password: "staff123", role: "Staff" }
];

var orders = [];
var sales = [];
var adjustments = [];
var nextOrderId = 1;
var nextSaleId = 1;
var nextAdjustmentId = 1;


// ============================================================
//  STATE
// ============================================================
var currentPage = "dashboard";
var categoryFilter = "All";
var sortColumn = null;
var sortDirection = "asc";
var editingProductIndex = null;
var editingUserIndex = null;
var transactionType = null;
var transactionProductIndex = null;
var adjustmentProductIndex = null;
var pendingDeleteAction = null;
var loggedInUser = null;
var activityLog = [];
var toastTimer = null;


// ============================================================
//  PERMISSIONS (mirrors C# User methods)
// ============================================================
function canModifyPrices() {
    return loggedInUser && loggedInUser.role === "Admin";
}

function canViewFinancials() {
    return loggedInUser && loggedInUser.role === "Admin";
}

function canManageUsers() {
    return loggedInUser && loggedInUser.role === "Admin";
}


// ============================================================
//  ACTIVITY LOG
// ============================================================
function logActivity(actionText) {
    var who = loggedInUser ? loggedInUser.userName : "system";
    activityLog.unshift({ action: actionText, user: who, time: timeNow() });
    if (activityLog.length > 50) activityLog.pop();
    renderActivityLog();
}

function renderActivityLog() {
    var list = document.getElementById("activityList");
    if (!activityLog.length) {
        list.innerHTML = '<div class="activity-empty">No activity yet</div>';
        return;
    }
    var html = "";
    for (var i = 0; i < activityLog.length; i++) {
        var entry = activityLog[i];
        html += '<div class="activity-entry">';
        html += '<div class="activity-action"><span class="activity-username">@' + escapeHTML(entry.user) + '</span> ' + escapeHTML(entry.action) + '</div>';
        html += '<div class="activity-time">' + entry.time + '</div>';
        html += '</div>';
    }
    list.innerHTML = html;
}

function toggleActivityLog() {
    document.getElementById("activityPanel").classList.toggle("collapsed");
    document.getElementById("logToggleButton").classList.toggle("visible");
}


// ============================================================
//  TOAST
// ============================================================
function showToast(message) {
    var toast = document.getElementById("toast");
    toast.textContent = message;
    toast.classList.add("visible");
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function() { toast.classList.remove("visible"); }, 2500);
}


// ============================================================
//  LOGIN
// ============================================================
function attemptLogin() {
    var username = document.getElementById("loginUsername").value.trim().toLowerCase();
    var password = document.getElementById("loginPassword").value;
    var errorBox = document.getElementById("loginError");

    var foundUser = null;
    for (var i = 0; i < users.length; i++) {
        if (users[i].userName.toLowerCase() === username && users[i].password === password) {
            foundUser = users[i];
            break;
        }
    }

    if (!foundUser) {
        errorBox.textContent = "Invalid username or password";
        errorBox.classList.add("visible");
        return;
    }

    errorBox.classList.remove("visible");
    loggedInUser = foundUser;
    document.getElementById("loginScreen").classList.add("hidden");
    document.getElementById("loggedInName").textContent = foundUser.firstName + " " + foundUser.lastName;
    document.getElementById("loggedInRole").textContent = foundUser.role.toUpperCase() + " · @" + foundUser.userName;
    document.getElementById("loginPassword").value = "";
    logActivity("logged in");
    showPage("dashboard");
}

function logout() {
    logActivity("logged out");
    loggedInUser = null;
    document.getElementById("loginScreen").classList.remove("hidden");
    document.getElementById("loginUsername").value = "";
}

document.getElementById("loginPassword").addEventListener("keydown", function(e) {
    if (e.key === "Enter") attemptLogin();
});


// ============================================================
//  NAVIGATION
// ============================================================
function showPage(pageName) {
    currentPage = pageName;
    document.querySelectorAll(".page").forEach(function(el) { el.classList.remove("active"); });
    document.getElementById("page-" + pageName).classList.add("active");
    document.querySelectorAll(".nav-button").forEach(function(el) { el.classList.remove("active"); });
    document.getElementById("nav-" + pageName).classList.add("active");

    if (pageName === "dashboard") renderDashboard();
    if (pageName === "products") renderProductTable();
    if (pageName === "users") renderUserCards();
    if (pageName === "transactions") renderTransactionsPage();
}


// ============================================================
//  DASHBOARD
// ============================================================
function renderDashboard() {
    var totalValue = 0, lowStockItems = [], rawCount = 0, finishedCount = 0;
    for (var i = 0; i < products.length; i++) {
        var p = products[i];
        totalValue += p.price * p.quantity;
        if (p.category === "RawMaterial") rawCount++; else finishedCount++;
        if (p.quantity <= p.reorderLevel) lowStockItems.push(p);
    }
    var totalRevenue = sales.reduce(function(s, x) { return s + x.income; }, 0);

    // Stat cards
    var statsHTML = "";
    statsHTML += makeStatCardHTML("Total Products", products.length, rawCount + " raw · " + finishedCount + " finished", "#D4820E");
    statsHTML += makeStatCardHTML("Inventory Value", formatMoney(totalValue), "", "#3A8FD6");
    statsHTML += makeStatCardHTML("Low Stock", lowStockItems.length, lowStockItems.length ? lowStockItems.length + " below reorder" : "All stocked", lowStockItems.length ? "#CF4040" : "#2E9B63");
    if (canViewFinancials()) {
        statsHTML += makeStatCardHTML("Total Revenue", formatMoney(totalRevenue), sales.length + " sales · " + orders.length + " orders", "#2E9B63");
    } else {
        statsHTML += makeStatCardHTML("Accounts", users.length, "Active users", "#8B6FC0");
    }
    document.getElementById("dashboardStats").innerHTML = statsHTML;

    // Low stock alerts
    var alertsHTML = "";
    if (lowStockItems.length) {
        alertsHTML += '<h2 class="section-heading">⚠️ Low Stock Alerts</h2><div class="alert-grid">';
        for (var i = 0; i < lowStockItems.length; i++) {
            var p = lowStockItems[i];
            var percent = Math.min(100, p.reorderLevel > 0 ? (p.quantity / p.reorderLevel) * 100 : 0);
            var critical = percent < 50;
            var color = critical ? "#CF4040" : "#D4820E";
            alertsHTML += '<div class="alert-card' + (critical ? ' critical' : '') + '">';
            alertsHTML += '<div style="display:flex;justify-content:space-between;margin-bottom:6px"><div class="alert-card-name">' + escapeHTML(p.name) + '</div><span class="badge badge-low">' + (critical ? "CRITICAL" : "LOW") + '</span></div>';
            alertsHTML += '<div class="alert-card-meta"><span>Qty: <strong style="color:' + color + '">' + p.quantity + '</strong></span><span>Reorder: ' + p.reorderLevel + '</span></div>';
            alertsHTML += '<div class="progress-track"><div class="progress-fill" style="width:' + percent + '%;background:' + color + '"></div></div>';
            alertsHTML += '</div>';
        }
        alertsHTML += '</div>';
    }
    document.getElementById("dashboardAlerts").innerHTML = alertsHTML;

    // Categories
    var categories = [
        { key: "RawMaterial", label: "Raw Materials", badge: "raw" },
        { key: "Finished", label: "Finished Goods", badge: "finished" }
    ];
    var categoriesHTML = "";
    for (var c = 0; c < categories.length; c++) {
        var cat = categories[c];
        var items = products.filter(function(p) { return p.category === cat.key; });
        var value = items.reduce(function(s, p) { return s + p.price * p.quantity; }, 0);
        var totalQty = items.reduce(function(s, p) { return s + p.quantity; }, 0);
        categoriesHTML += '<div class="category-card">';
        categoriesHTML += '<div class="category-header"><span class="badge badge-' + cat.badge + '">' + cat.label + '</span><span style="font-size:12px;color:var(--text-dim);font-family:var(--font-mono)">' + items.length + ' SKUs</span></div>';
        categoriesHTML += '<div class="category-stats">';
        categoriesHTML += '<div><div class="category-big-number">' + formatMoney(value) + '</div><div class="category-small-label">TOTAL VALUE</div></div>';
        categoriesHTML += '<div class="category-divider"><div class="category-big-number">' + totalQty.toLocaleString() + '</div><div class="category-small-label">TOTAL UNITS</div></div>';
        categoriesHTML += '</div></div>';
    }
    document.getElementById("dashboardCategories").innerHTML = categoriesHTML;
}

function makeStatCardHTML(label, value, sub, color) {
    var html = '<div class="stat-card">';
    html += '<div class="stat-card-label">' + label + '</div>';
    html += '<div class="stat-card-value" style="color:' + color + '">' + value + '</div>';
    if (sub) html += '<div class="stat-card-sub">' + sub + '</div>';
    html += '<div class="stat-card-bar" style="background:linear-gradient(90deg,' + color + ',transparent)"></div>';
    html += '</div>';
    return html;
}


// ============================================================
//  PRODUCTS TABLE
// ============================================================
function renderProductTable() {
    var rawCount = products.filter(function(p) { return p.category === "RawMaterial"; }).length;
    document.getElementById("productCount").textContent = products.length + " items — " + rawCount + " raw / " + (products.length - rawCount) + " finished";

    // Permission notice
    var notice = document.getElementById("productNotice");
    if (loggedInUser.role === "Staff") {
        notice.textContent = "🔒 Staff access — you can record orders & sales but cannot modify prices or delete products.";
        notice.style.display = "block";
    } else {
        notice.style.display = "none";
    }

    // Filter and sort
    var query = document.getElementById("searchInput").value.toLowerCase();
    var filtered = [];
    for (var i = 0; i < products.length; i++) {
        var p = products[i];
        if (categoryFilter !== "All" && p.category !== categoryFilter) continue;
        if (query && p.name.toLowerCase().indexOf(query) < 0 && p.sku.toLowerCase().indexOf(query) < 0 && p.manufacturer.toLowerCase().indexOf(query) < 0) continue;
        filtered.push({ product: p, index: i });
    }
    if (sortColumn) {
        filtered.sort(function(a, b) {
            var va = a.product[sortColumn], vb = b.product[sortColumn];
            if (typeof va === "string") { va = va.toLowerCase(); vb = vb.toLowerCase(); }
            if (va < vb) return sortDirection === "asc" ? -1 : 1;
            if (va > vb) return sortDirection === "asc" ? 1 : -1;
            return 0;
        });
    }

    // Sort indicators
    ["name", "category", "manufacturer", "price", "quantity"].forEach(function(col) {
        document.getElementById("sort-" + col).textContent = sortColumn === col ? (sortDirection === "asc" ? " ▼" : " ▲") : "";
    });

    var tbody = document.getElementById("productTableBody");
    if (!filtered.length) {
        tbody.innerHTML = '<tr><td colspan="9" class="empty-state">No products found.</td></tr>';
        return;
    }

    var rows = "";
    for (var i = 0; i < filtered.length; i++) {
        var p = filtered[i].product, idx = filtered[i].index;
        var isLow = p.quantity <= p.reorderLevel;
        rows += "<tr>";
        rows += '<td><span class="product-name-cell">' + escapeHTML(p.name) + '</span></td>';
        rows += '<td><code class="sku-pill">' + p.sku + '</code></td>';
        rows += '<td><span class="badge badge-' + (p.category === "RawMaterial" ? "raw" : "finished") + '">' + (p.category === "RawMaterial" ? "RAW" : "FINISHED") + '</span></td>';
        rows += '<td><span class="' + (p.manufacturer === "SELF" ? "manufacturer-self" : "manufacturer-external") + '">' + escapeHTML(p.manufacturer) + '</span></td>';
        rows += '<td>' + formatMoney(p.price) + '</td>';
        rows += '<td style="font-weight:600">' + p.quantity.toLocaleString() + '</td>';
        rows += '<td>' + p.reorderLevel.toLocaleString() + '</td>';
        rows += '<td><span class="badge badge-' + (isLow ? "low" : "ok") + '">' + (isLow ? "LOW" : "IN STOCK") + '</span></td>';
        rows += '<td style="text-align:right">';
        rows += '<button class="icon-button" onclick="openTransactionModal(\'order\', ' + idx + ')" title="Record Order">📥</button>';
        rows += '<button class="icon-button" onclick="openTransactionModal(\'sale\', ' + idx + ')" title="Record Sale">💰</button>';
        if (canModifyPrices()) {
            rows += '<button class="icon-button" onclick="openAdjustmentModal(' + idx + ')" title="Adjust Price">💲</button>';
            rows += '<button class="icon-button" onclick="openProductModal(' + idx + ')" title="Edit">✏️</button>';
            rows += '<button class="icon-button danger" onclick="confirmDeleteProduct(' + idx + ')" title="Delete">🗑️</button>';
        }
        rows += "</td></tr>";
    }
    tbody.innerHTML = rows;
}

function setCategoryFilter(category) {
    categoryFilter = category;
    document.querySelectorAll(".filter-button").forEach(function(b) {
        b.classList.toggle("active", b.getAttribute("data-filter") === category);
    });
    renderProductTable();
}

function toggleSort(column) {
    if (sortColumn === column) {
        sortDirection = sortDirection === "asc" ? "desc" : "asc";
    } else {
        sortColumn = column;
        sortDirection = "asc";
    }
    renderProductTable();
}


// ============================================================
//  PRODUCT MODAL
// ============================================================
function openProductModal(indexOrAdd) {
    if (!canModifyPrices()) return;
    editingProductIndex = (indexOrAdd === "add") ? null : indexOrAdd;
    var adding = editingProductIndex === null;

    document.getElementById("productModalTitle").textContent = adding ? "Add Product" : "Edit Product";
    document.getElementById("productSaveButton").textContent = adding ? "Add Product" : "Save Changes";

    if (adding) {
        document.getElementById("inputName").value = "";
        document.getElementById("inputCategory").value = "RawMaterial";
        document.getElementById("inputPrice").value = "";
        document.getElementById("inputQuantity").value = "";
        document.getElementById("inputReorder").value = "";
        document.getElementById("inputManufacturer").value = "";
    } else {
        var p = products[editingProductIndex];
        document.getElementById("inputName").value = p.name;
        document.getElementById("inputCategory").value = p.category;
        document.getElementById("inputPrice").value = p.price;
        document.getElementById("inputQuantity").value = p.quantity;
        document.getElementById("inputReorder").value = p.reorderLevel;
        document.getElementById("inputManufacturer").value = p.manufacturer === "SELF" ? "" : p.manufacturer;
    }
    document.getElementById("productOverlay").classList.add("visible");
}

function closeProductModal() {
    document.getElementById("productOverlay").classList.remove("visible");
}

function saveProduct() {
    var name = document.getElementById("inputName").value.trim();
    if (!name) { alert("Please enter a product name."); return; }

    var product = {
        sku: editingProductIndex === null ? makeSKU() : products[editingProductIndex].sku,
        name: name,
        category: document.getElementById("inputCategory").value,
        price: parseFloat(document.getElementById("inputPrice").value) || 0,
        quantity: parseInt(document.getElementById("inputQuantity").value) || 0,
        reorderLevel: parseInt(document.getElementById("inputReorder").value) || 0,
        manufacturer: document.getElementById("inputManufacturer").value.trim() || "SELF"
    };

    if (editingProductIndex === null) {
        products.push(product);
        showToast('Added "' + name + '"');
        logActivity('added product "' + name + '"');
    } else {
        products[editingProductIndex] = product;
        showToast('Updated "' + name + '"');
        logActivity('updated product "' + name + '"');
    }

    closeProductModal();
    renderProductTable();
}

function confirmDeleteProduct(index) {
    document.getElementById("confirmMessage").textContent = 'Remove "' + products[index].name + '" from inventory?';
    var productName = products[index].name;
    pendingDeleteAction = function() {
        showToast('Removed "' + productName + '"');
        logActivity('removed product "' + productName + '"');
        products.splice(index, 1);
        renderProductTable();
    };
    document.getElementById("confirmOverlay").classList.add("visible");
}


// ============================================================
//  TRANSACTION MODAL (Order / Sale)
// ============================================================
function openTransactionModal(type, productIndex) {
    transactionType = type;
    transactionProductIndex = productIndex;
    var p = products[productIndex];
    var isOrder = type === "order";

    document.getElementById("transactionTitle").textContent = isOrder ? "Record Order (Restock)" : "Record Sale";
    var saveBtn = document.getElementById("transactionSaveButton");
    saveBtn.textContent = isOrder ? "Record Order" : "Record Sale";
    saveBtn.className = isOrder ? "primary-button purple" : "primary-button success";

    document.getElementById("transactionProductName").textContent = p.name;
    document.getElementById("transactionProductSku").textContent = p.sku;
    document.getElementById("transactionCurrentStock").textContent = p.quantity.toLocaleString();
    document.getElementById("transactionAmount").value = "";
    document.getElementById("transactionPriceLabel").textContent = isOrder ? "Cost Per Unit ($)" : "Sale Price Per Unit ($)";
    document.getElementById("transactionPrice").value = p.price.toFixed(2);

    document.getElementById("transactionOverlay").classList.add("visible");
}

function closeTransactionModal() {
    document.getElementById("transactionOverlay").classList.remove("visible");
}

function saveTransaction() {
    var amount = parseInt(document.getElementById("transactionAmount").value);
    var price = parseFloat(document.getElementById("transactionPrice").value);

    if (!amount || amount <= 0) { alert("Please enter a valid amount."); return; }
    if (isNaN(price) || price < 0) { alert("Please enter a valid price."); return; }

    var p = products[transactionProductIndex];
    var total = amount * price;

    if (transactionType === "order") {
        orders.push({ orderId: nextOrderId++, sku: p.sku, userName: loggedInUser.userName, amount: amount, cost: total });
        p.quantity += amount;
        showToast("Recorded order: +" + amount + " units");
        logActivity('ordered +' + amount + ' units of "' + p.name + '" (' + formatMoney(total) + ')');
    } else {
        if (amount > p.quantity) { alert("Cannot sell more than current stock (" + p.quantity + ")."); return; }
        sales.push({ orderId: nextSaleId++, sku: p.sku, userName: loggedInUser.userName, amount: amount, income: total });
        p.quantity -= amount;
        showToast("Recorded sale: -" + amount + " units");
        logActivity('sold ' + amount + ' units of "' + p.name + '" (' + formatMoney(total) + ')');
    }

    closeTransactionModal();
    renderProductTable();
    if (currentPage === "dashboard") renderDashboard();
}


// ============================================================
//  ADJUSTMENT MODAL (Price Change)
// ============================================================
function openAdjustmentModal(productIndex) {
    if (!canModifyPrices()) return;
    adjustmentProductIndex = productIndex;
    var p = products[productIndex];
    document.getElementById("adjustmentProductName").textContent = p.name;
    document.getElementById("adjustmentOldPrice").textContent = formatMoney(p.price);
    document.getElementById("adjustmentNewPrice").value = "";
    document.getElementById("adjustmentReason").value = "";
    document.getElementById("adjustmentOverlay").classList.add("visible");
}

function closeAdjustmentModal() {
    document.getElementById("adjustmentOverlay").classList.remove("visible");
}

function saveAdjustment() {
    var newPrice = parseFloat(document.getElementById("adjustmentNewPrice").value);
    var reason = document.getElementById("adjustmentReason").value.trim();
    if (isNaN(newPrice) || newPrice < 0) { alert("Please enter a valid new price."); return; }
    if (!reason) { alert("Please provide a reason for the price change."); return; }

    var p = products[adjustmentProductIndex];
    var oldPrice = p.price;
    adjustments.push({ adjustmentId: nextAdjustmentId++, sku: p.sku, userName: loggedInUser.userName, oldPrice: oldPrice, newPrice: newPrice, reason: reason });
    p.price = newPrice;

    showToast("Price adjusted for " + p.name);
    logActivity('adjusted "' + p.name + '" from ' + formatMoney(oldPrice) + ' to ' + formatMoney(newPrice));
    closeAdjustmentModal();
    renderProductTable();
}


// ============================================================
//  TRANSACTIONS PAGE
// ============================================================
function renderTransactionsPage() {
    if (!canViewFinancials()) {
        document.getElementById("transactionsContent").innerHTML = '<div class="permission-notice">🔒 You need Admin access to view financial transactions.</div>';
        return;
    }

    var allTransactions = [];
    orders.forEach(function(o) { allTransactions.push({ type: "order", data: o }); });
    sales.forEach(function(s) { allTransactions.push({ type: "sale", data: s }); });
    adjustments.forEach(function(a) { allTransactions.push({ type: "adjustment", data: a }); });

    if (!allTransactions.length) {
        document.getElementById("transactionsContent").innerHTML = '<div class="table-container"><div class="empty-state">No transactions yet. Go to Products and record an order or sale.</div></div>';
        return;
    }

    var totalRevenue = sales.reduce(function(s, x) { return s + x.income; }, 0);
    var totalCosts = orders.reduce(function(s, x) { return s + x.cost; }, 0);
    var profit = totalRevenue - totalCosts;

    // Summary stats
    var summary = '<div class="stats-grid">';
    summary += makeStatCardHTML("Total Revenue", formatMoney(totalRevenue), sales.length + " sales", "#2E9B63");
    summary += makeStatCardHTML("Total Costs", formatMoney(totalCosts), orders.length + " orders", "#CF4040");
    summary += makeStatCardHTML("Net Profit", formatMoney(profit), profit >= 0 ? "In the green" : "Loss", profit >= 0 ? "#2E9B63" : "#CF4040");
    summary += makeStatCardHTML("Adjustments", adjustments.length, "Price changes logged", "#D4820E");
    summary += '</div>';

    // Transaction ledger
    var rows = "";
    for (var i = allTransactions.length - 1; i >= 0; i--) {
        var tx = allTransactions[i];
        var product = products.find(function(p) { return p.sku === tx.data.sku; });
        var productName = product ? product.name : "(deleted product)";

        if (tx.type === "order") {
            rows += '<tr><td><span class="badge badge-order">ORDER</span></td>';
            rows += '<td><code class="sku-pill">#' + tx.data.orderId + '</code></td>';
            rows += '<td><span class="product-name-cell">' + escapeHTML(productName) + '</span></td>';
            rows += '<td>@' + escapeHTML(tx.data.userName) + '</td>';
            rows += '<td>+' + tx.data.amount + ' units (restock)</td>';
            rows += '<td style="color:#CF4040;font-weight:600">-' + formatMoney(tx.data.cost) + '</td></tr>';
        } else if (tx.type === "sale") {
            rows += '<tr><td><span class="badge badge-sale">SALE</span></td>';
            rows += '<td><code class="sku-pill">#' + tx.data.orderId + '</code></td>';
            rows += '<td><span class="product-name-cell">' + escapeHTML(productName) + '</span></td>';
            rows += '<td>@' + escapeHTML(tx.data.userName) + '</td>';
            rows += '<td>-' + tx.data.amount + ' units sold</td>';
            rows += '<td style="color:#2E9B63;font-weight:600">+' + formatMoney(tx.data.income) + '</td></tr>';
        } else {
            rows += '<tr><td><span class="badge badge-adjust">ADJUSTMENT</span></td>';
            rows += '<td><code class="sku-pill">#' + tx.data.adjustmentId + '</code></td>';
            rows += '<td><span class="product-name-cell">' + escapeHTML(productName) + '</span></td>';
            rows += '<td>@' + escapeHTML(tx.data.userName) + '</td>';
            rows += '<td>' + escapeHTML(tx.data.reason) + '</td>';
            rows += '<td>' + formatMoney(tx.data.oldPrice) + ' → ' + formatMoney(tx.data.newPrice) + '</td></tr>';
        }
    }

    var table = '<div class="table-container"><table><thead><tr>';
    table += '<th>Type</th><th>ID</th><th>Product</th><th>User</th><th>Details</th><th>Amount</th>';
    table += '</tr></thead><tbody>' + rows + '</tbody></table></div>';

    document.getElementById("transactionsContent").innerHTML = summary + table;
}


// ============================================================
//  USERS
// ============================================================
function renderUserCards() {
    document.getElementById("userCount").textContent = users.length + " registered users";

    var notice = document.getElementById("userNotice");
    if (!canManageUsers()) {
        notice.textContent = "🔒 Staff access — account management is view-only.";
        notice.style.display = "block";
    } else {
        notice.style.display = "none";
    }
    document.getElementById("addUserButton").style.display = canManageUsers() ? "" : "none";

    var colors = { Admin: "#D4820E", Staff: "#3A8FD6" };
    var html = "";
    for (var i = 0; i < users.length; i++) {
        var u = users[i];
        var color = colors[u.role] || "#6B6F7A";
        var badgeClass = u.role === "Admin" ? "badge-admin" : "badge-staff";

        html += '<div class="user-card">';
        html += '<div class="user-avatar" style="border:1.5px solid ' + color + '40"><span style="color:' + color + '">' + u.firstName.charAt(0) + u.lastName.charAt(0) + '</span></div>';
        html += '<div style="flex:1;min-width:0">';
        html += '<div class="user-name">' + escapeHTML(u.firstName) + ' ' + escapeHTML(u.lastName) + '</div>';
        html += '<div class="user-handle">@' + u.userName + '</div>';
        html += '<div class="user-badge"><span class="badge ' + badgeClass + '">' + u.role + '</span></div>';
        html += '</div>';
        html += '<div class="user-actions">';
        if (canManageUsers()) {
            html += '<button class="icon-button" onclick="openUserModal(' + i + ')">✏️</button>';
            html += '<button class="icon-button danger" onclick="confirmDeleteUser(' + i + ')">🗑️</button>';
        } else {
            html += '<span style="color:var(--text-dim);font-size:11px">View only</span>';
        }
        html += '</div></div>';
    }
    document.getElementById("userGrid").innerHTML = html;
}


// ============================================================
//  USER MODAL
// ============================================================
function openUserModal(indexOrAdd) {
    if (!canManageUsers()) return;
    editingUserIndex = (indexOrAdd === "add") ? null : indexOrAdd;
    var adding = editingUserIndex === null;

    document.getElementById("userModalTitle").textContent = adding ? "Add User" : "Edit User";
    document.getElementById("userSaveButton").textContent = adding ? "Create User" : "Save Changes";

    if (adding) {
        document.getElementById("inputUsername").value = "";
        document.getElementById("inputFirstName").value = "";
        document.getElementById("inputLastName").value = "";
        document.getElementById("inputPassword").value = "";
        document.getElementById("inputRole").value = "Staff";
        document.getElementById("inputUsername").disabled = false;
    } else {
        var u = users[editingUserIndex];
        document.getElementById("inputUsername").value = u.userName;
        document.getElementById("inputFirstName").value = u.firstName;
        document.getElementById("inputLastName").value = u.lastName;
        document.getElementById("inputPassword").value = "";
        document.getElementById("inputRole").value = u.role;
        document.getElementById("inputUsername").disabled = true;
    }
    document.getElementById("userOverlay").classList.add("visible");
}

function closeUserModal() {
    document.getElementById("userOverlay").classList.remove("visible");
}

function saveUser() {
    var userName = document.getElementById("inputUsername").value.trim().toLowerCase();
    var firstName = document.getElementById("inputFirstName").value.trim();
    var lastName = document.getElementById("inputLastName").value.trim();
    var password = document.getElementById("inputPassword").value;
    var role = document.getElementById("inputRole").value;

    if (!userName || !firstName || !lastName) { alert("Please fill in username, first name, and last name."); return; }
    if (editingUserIndex === null && !password) { alert("Please set a password for new users."); return; }

    if (editingUserIndex === null) {
        for (var i = 0; i < users.length; i++) {
            if (users[i].userName === userName) { alert("That username is already taken."); return; }
        }
        users.push({ userName: userName, firstName: firstName, lastName: lastName, password: password, role: role });
        showToast("Created @" + userName);
        logActivity('created user @' + userName + ' (' + role + ')');
    } else {
        var u = users[editingUserIndex];
        u.firstName = firstName;
        u.lastName = lastName;
        u.role = role;
        if (password) u.password = password;
        showToast("Updated @" + u.userName);
        logActivity('updated user @' + u.userName);
    }

    closeUserModal();
    renderUserCards();
}

function confirmDeleteUser(index) {
    if (!canManageUsers()) return;
    if (users[index].userName === loggedInUser.userName) { alert("You cannot delete your own account."); return; }

    document.getElementById("confirmMessage").textContent = "Remove user @" + users[index].userName + "?";
    var userName = users[index].userName;
    pendingDeleteAction = function() {
        showToast("Removed @" + userName);
        logActivity('removed user @' + userName);
        users.splice(index, 1);
        renderUserCards();
    };
    document.getElementById("confirmOverlay").classList.add("visible");
}


// ============================================================
//  CONFIRM DIALOG
// ============================================================
function executeConfirm() {
    if (pendingDeleteAction) pendingDeleteAction();
    closeConfirm();
}

function closeConfirm() {
    document.getElementById("confirmOverlay").classList.remove("visible");
    pendingDeleteAction = null;
}


// ============================================================
//  CLOSE MODALS BY CLICKING BACKDROP
// ============================================================
document.getElementById("productOverlay").addEventListener("click", function(e) { if (e.target === this) closeProductModal(); });
document.getElementById("userOverlay").addEventListener("click", function(e) { if (e.target === this) closeUserModal(); });
document.getElementById("transactionOverlay").addEventListener("click", function(e) { if (e.target === this) closeTransactionModal(); });
document.getElementById("adjustmentOverlay").addEventListener("click", function(e) { if (e.target === this) closeAdjustmentModal(); });
document.getElementById("confirmOverlay").addEventListener("click", function(e) { if (e.target === this) closeConfirm(); });


// ============================================================
//  INIT
// ============================================================
renderActivityLog();
