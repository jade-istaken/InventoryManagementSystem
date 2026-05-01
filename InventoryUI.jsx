import { useState, useEffect, useRef, useCallback } from "react";

/* ─── Data layer mirrors C# classes exactly ─── */
const generateSKU = () => {
  const s = crypto.randomUUID().replace(/-/g, "").slice(0, 12).toUpperCase();
  return `${s.slice(0,4)}-${s.slice(4,8)}-${s.slice(8)}`;
};
const genUsername = (f, l) => f && l ? (f[0] + l).toUpperCase() : "";

const SEED_PRODUCTS = [
  { name: "Cold-Rolled Steel Sheet", sku: generateSKU(), category: "RawMaterial", price: 18.75, quantity: 1240, reorderLevel: 300, manufacturer: "Nippon Steel" },
  { name: "Copper Wire 14AWG", sku: generateSKU(), category: "RawMaterial", price: 6.30, quantity: 85, reorderLevel: 200, manufacturer: "Southwire" },
  { name: "Servo Motor SM-400", sku: generateSKU(), category: "Finished", price: 312.00, quantity: 44, reorderLevel: 20, manufacturer: "SELF" },
  { name: "Silicone Gasket Ring", sku: generateSKU(), category: "RawMaterial", price: 1.10, quantity: 6800, reorderLevel: 1500, manufacturer: "Parker Hannifin" },
  { name: "PCB Assembly Rev.7", sku: generateSKU(), category: "Finished", price: 67.50, quantity: 190, reorderLevel: 50, manufacturer: "SELF" },
  { name: "Hydraulic Cylinder HC-20", sku: generateSKU(), category: "Finished", price: 489.00, quantity: 12, reorderLevel: 15, manufacturer: "Bosch Rexroth" },
  { name: "Aluminum Extrusion 6061", sku: generateSKU(), category: "RawMaterial", price: 22.40, quantity: 520, reorderLevel: 150, manufacturer: "Alcoa" },
  { name: "Power Supply Unit 24V", sku: generateSKU(), category: "Finished", price: 145.00, quantity: 78, reorderLevel: 25, manufacturer: "SELF" },
];
const SEED_USERS = [
  { firstName: "Diana", lastName: "Kovacs", privilege: "Administrator" },
  { firstName: "Marcus", lastName: "Obi", privilege: "Staff" },
  { firstName: "Sarah", lastName: "Lindgren", privilege: "Staff" },
  { firstName: "James", lastName: "Whitfield", privilege: "Administrator" },
  { firstName: "Priya", lastName: "Nair", privilege: "Staff" },
];

/* ─── Icons (inline SVGs) ─── */
const I = {
  grid: <svg width="18" height="18" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" viewBox="0 0 24 24"><rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/></svg>,
  box: <svg width="18" height="18" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" viewBox="0 0 24 24"><path d="M21 16V8a2 2 0 00-1-1.73l-7-4a2 2 0 00-2 0l-7 4A2 2 0 002 8v8a2 2 0 001 1.73l7 4a2 2 0 002 0l7-4A2 2 0 0021 16z"/><polyline points="3.27 6.96 12 12.01 20.73 6.96"/><line x1="12" y1="22.08" x2="12" y2="12"/></svg>,
  users: <svg width="18" height="18" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" viewBox="0 0 24 24"><path d="M17 21v-2a4 4 0 00-4-4H5a4 4 0 00-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 00-3-3.87M16 3.13a4 4 0 010 7.75"/></svg>,
  plus: <svg width="15" height="15" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" viewBox="0 0 24 24"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>,
  x: <svg width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" viewBox="0 0 24 24"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>,
  edit: <svg width="13" height="13" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" viewBox="0 0 24 24"><path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>,
  trash: <svg width="13" height="13" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" viewBox="0 0 24 24"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a2 2 0 012-2h4a2 2 0 012 2v2"/></svg>,
  search: <svg width="15" height="15" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" viewBox="0 0 24 24"><circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/></svg>,
  alert: <svg width="15" height="15" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" viewBox="0 0 24 24"><path d="M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>,
  chev: (d) => <svg width="12" height="12" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" viewBox="0 0 24 24" style={{transform:d==="up"?"rotate(180deg)":"none",transition:"transform .2s"}}><polyline points="6 9 12 15 18 9"/></svg>,
  dollar: <svg width="18" height="18" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" viewBox="0 0 24 24"><line x1="12" y1="1" x2="12" y2="23"/><path d="M17 5H9.5a3.5 3.5 0 000 7h5a3.5 3.5 0 010 7H6"/></svg>,
  shield: <svg width="18" height="18" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" viewBox="0 0 24 24"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/></svg>,
};

/* ─── Micro-components ─── */
const Badge = ({ text, tone }) => {
  const c = { raw: "#D4820E", fin: "#3A8FD6", low: "#CF4040", ok: "#2E9B63", admin: "#D4820E", staff: "#3A8FD6", guest: "#7C7F86" };
  const col = c[tone] || "#7C7F86";
  return <span style={{ display:"inline-flex", alignItems:"center", gap:4, padding:"3px 10px", borderRadius:4, fontSize:11, fontWeight:700, letterSpacing:".04em", textTransform:"uppercase", fontFamily:"'IBM Plex Mono',monospace", color:col, background:col+"18", border:`1px solid ${col}30` }}>{text}</span>;
};

const StatTile = ({ icon, label, value, sub, accent }) => (
  <div style={{ background:"#1C1E24", borderRadius:10, padding:"22px 24px", border:"1px solid #2A2D35", position:"relative", overflow:"hidden" }}>
    <div style={{ position:"absolute", top:16, right:18, color:accent, opacity:.25 }}>{icon}</div>
    <div style={{ fontSize:11, fontWeight:600, color:"#6B6F7A", textTransform:"uppercase", letterSpacing:".08em", marginBottom:10, fontFamily:"'IBM Plex Mono',monospace" }}>{label}</div>
    <div style={{ fontSize:30, fontWeight:800, color:"#EAECF0", letterSpacing:"-.03em", fontFamily:"'Outfit',sans-serif" }}>{value}</div>
    {sub && <div style={{ fontSize:12, color:"#6B6F7A", marginTop:6 }}>{sub}</div>}
    <div style={{ position:"absolute", bottom:0, left:0, right:0, height:3, background:`linear-gradient(90deg, ${accent}, transparent)` }}/>
  </div>
);

function Modal({ open, onClose, title, children }) {
  if (!open) return null;
  return (
    <div onClick={onClose} style={{ position:"fixed", inset:0, background:"rgba(0,0,0,.65)", backdropFilter:"blur(6px)", display:"flex", alignItems:"center", justifyContent:"center", zIndex:1000, animation:"fadeIn .2s" }}>
      <div onClick={e=>e.stopPropagation()} style={{ background:"#1C1E24", border:"1px solid #2A2D35", borderRadius:14, padding:"28px 32px", width:"92%", maxWidth:560, maxHeight:"88vh", overflow:"auto", boxShadow:"0 24px 80px rgba(0,0,0,.5)" }}>
        <div style={{ display:"flex", justifyContent:"space-between", alignItems:"center", marginBottom:24 }}>
          <h3 style={{ margin:0, fontSize:18, fontWeight:700, color:"#EAECF0", fontFamily:"'Outfit',sans-serif" }}>{title}</h3>
          <button onClick={onClose} style={{ background:"none", border:"none", color:"#6B6F7A", cursor:"pointer", padding:4 }}>{I.x}</button>
        </div>
        {children}
      </div>
    </div>
  );
}

function ConfirmDialog({ open, message, onConfirm, onCancel }) {
  if (!open) return null;
  return (
    <div onClick={onCancel} style={{ position:"fixed", inset:0, background:"rgba(0,0,0,.65)", backdropFilter:"blur(6px)", display:"flex", alignItems:"center", justifyContent:"center", zIndex:1100 }}>
      <div onClick={e=>e.stopPropagation()} style={{ background:"#1C1E24", border:"1px solid #2A2D35", borderRadius:14, padding:"28px 32px", width:"90%", maxWidth:400, boxShadow:"0 24px 80px rgba(0,0,0,.5)" }}>
        <p style={{ color:"#EAECF0", fontSize:15, margin:"0 0 24px", lineHeight:1.5 }}>{message}</p>
        <div style={{ display:"flex", justifyContent:"flex-end", gap:10 }}>
          <button onClick={onCancel} style={S.ghostBtn}>Cancel</button>
          <button onClick={onConfirm} style={{ ...S.primaryBtn, background:"#CF4040" }}>Delete</button>
        </div>
      </div>
    </div>
  );
}

/* ════════════════════════════════════════
   MAIN APPLICATION
   ════════════════════════════════════════ */
export default function App() {
  const [view, setView] = useState("dashboard");
  const [products, setProducts] = useState(SEED_PRODUCTS);
  const [users, setUsers] = useState(SEED_USERS);

  // Product state
  const [search, setSearch] = useState("");
  const [catFilter, setCatFilter] = useState("All");
  const [sortCol, setSortCol] = useState(null);
  const [sortDir, setSortDir] = useState("asc");
  const [prodModal, setProdModal] = useState(null);
  const [pForm, setPForm] = useState({ name:"", category:"RawMaterial", price:"", quantity:"", reorderLevel:"", manufacturer:"" });

  // User state
  const [userModal, setUserModal] = useState(null);
  const [uForm, setUForm] = useState({ firstName:"", lastName:"", privilege:"Staff" });

  // UI state
  const [toast, setToast] = useState(null);
  const [confirm, setConfirm] = useState(null);
  const timerRef = useRef();

  const flash = (msg) => {
    setToast(msg);
    clearTimeout(timerRef.current);
    timerRef.current = setTimeout(() => setToast(null), 2800);
  };

  /* ── Product ops ── */
  const openAddProd = () => { setPForm({ name:"", category:"RawMaterial", price:"", quantity:"", reorderLevel:"", manufacturer:"" }); setProdModal("add"); };
  const openEditProd = (i) => { const p=products[i]; setPForm({ name:p.name, category:p.category, price:String(p.price), quantity:String(p.quantity), reorderLevel:String(p.reorderLevel), manufacturer:p.manufacturer }); setProdModal(i); };
  const saveProd = () => {
    if (!pForm.name.trim()) return;
    const entry = { name:pForm.name.trim(), sku: prodModal==="add" ? generateSKU() : products[prodModal].sku, category:pForm.category, price:parseFloat(pForm.price)||0, quantity:parseInt(pForm.quantity)||0, reorderLevel:parseInt(pForm.reorderLevel)||0, manufacturer:pForm.manufacturer.trim()||"SELF" };
    if (prodModal==="add") { setProducts([...products, entry]); flash(`Added "${entry.name}"`); }
    else { const c=[...products]; c[prodModal]=entry; setProducts(c); flash(`Updated "${entry.name}"`); }
    setProdModal(null);
  };
  const confirmDeleteProd = (i) => setConfirm({ message:`Remove "${products[i].name}" from inventory?`, action:()=>{ flash(`Removed "${products[i].name}"`); setProducts(products.filter((_,idx)=>idx!==i)); setConfirm(null); }});

  /* ── User ops ── */
  const openAddUser = () => { setUForm({ firstName:"", lastName:"", privilege:"Staff" }); setUserModal("add"); };
  const openEditUser = (i) => { const u=users[i]; setUForm({ firstName:u.firstName, lastName:u.lastName, privilege:u.privilege }); setUserModal(i); };
  const saveUser = () => {
    if (!uForm.firstName.trim()||!uForm.lastName.trim()) return;
    const entry = { firstName:uForm.firstName.trim(), lastName:uForm.lastName.trim(), privilege:uForm.privilege };
    if (userModal==="add") { setUsers([...users, entry]); flash(`Created @${genUsername(entry.firstName,entry.lastName)}`); }
    else { const c=[...users]; c[userModal]=entry; setUsers(c); flash("User updated"); }
    setUserModal(null);
  };
  const confirmDeleteUser = (i) => setConfirm({ message:`Remove user @${genUsername(users[i].firstName,users[i].lastName)}?`, action:()=>{ flash(`Removed @${genUsername(users[i].firstName,users[i].lastName)}`); setUsers(users.filter((_,idx)=>idx!==i)); setConfirm(null); }});

  /* ── Filter/sort ── */
  const filtered = products.map((p,i)=>({...p,_i:i})).filter(p=>{
    if (catFilter!=="All" && p.category!==catFilter) return false;
    if (search) { const q=search.toLowerCase(); return p.name.toLowerCase().includes(q)||p.sku.toLowerCase().includes(q)||p.manufacturer.toLowerCase().includes(q); }
    return true;
  }).sort((a,b)=>{
    if (!sortCol) return 0;
    let va=a[sortCol], vb=b[sortCol];
    if (typeof va==="string") { va=va.toLowerCase(); vb=vb.toLowerCase(); }
    return va<vb?(sortDir==="asc"?-1:1):va>vb?(sortDir==="asc"?1:-1):0;
  });

  const toggleSort = (col) => { if (sortCol===col) setSortDir(d=>d==="asc"?"desc":"asc"); else { setSortCol(col); setSortDir("asc"); }};
  const SortTH = ({col,children}) => (
    <th style={S.th} onClick={()=>toggleSort(col)}>
      <span style={{display:"inline-flex",alignItems:"center",gap:4,cursor:"pointer",userSelect:"none"}}>{children}{sortCol===col && I.chev(sortDir==="asc"?"down":"up")}</span>
    </th>
  );

  /* ── Stats ── */
  const totalVal = products.reduce((s,p)=>s+p.price*p.quantity,0);
  const lowStock = products.filter(p=>p.quantity<=p.reorderLevel);
  const rawCount = products.filter(p=>p.category==="RawMaterial").length;
  const finCount = products.filter(p=>p.category==="Finished").length;

  const navItems = [
    { id:"dashboard", icon:I.grid, label:"Dashboard" },
    { id:"products", icon:I.box, label:"Products" },
    { id:"users", icon:I.users, label:"Accounts" },
  ];

  return (
    <div style={S.root}>
      <style>{`
        @import url('https://fonts.googleapis.com/css2?family=Outfit:wght@400;500;600;700;800&family=IBM+Plex+Mono:wght@400;500;600;700&display=swap');
        @keyframes fadeIn { from{opacity:0;transform:translateY(8px)} to{opacity:1;transform:translateY(0)} }
        @keyframes slideToast { from{opacity:0;transform:translate(-50%,16px)} to{opacity:1;transform:translate(-50%,0)} }
        @keyframes pulse { 0%,100%{opacity:1} 50%{opacity:.5} }
        *::-webkit-scrollbar { width:6px; }
        *::-webkit-scrollbar-track { background:transparent; }
        *::-webkit-scrollbar-thumb { background:#2A2D35; border-radius:3px; }
        input:focus, select:focus { border-color:#D4820E !important; outline:none; }
        tr:hover td { background:#22242C !important; }
        button:hover { opacity:.88; }
      `}</style>

      {/* ═══ SIDEBAR ═══ */}
      <aside style={S.sidebar}>
        <div style={S.brand}>
          <div style={S.brandMark}>
            <svg width="24" height="24" fill="none" stroke="#D4820E" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" viewBox="0 0 24 24">
              <path d="M21 16V8a2 2 0 00-1-1.73l-7-4a2 2 0 00-2 0l-7 4A2 2 0 002 8v8a2 2 0 001 1.73l7 4a2 2 0 002 0l7-4A2 2 0 0021 16z"/>
              <polyline points="7.5 4.21 12 6.81 16.5 4.21"/>
              <polyline points="7.5 19.79 7.5 14.6 3 12"/>
              <polyline points="21 12 16.5 14.6 16.5 19.79"/>
              <polyline points="3.27 6.96 12 12.01 20.73 6.96"/>
              <line x1="12" y1="22.08" x2="12" y2="12"/>
            </svg>
          </div>
          <div>
            <div style={{ fontSize:17, fontWeight:800, color:"#EAECF0", letterSpacing:"-.02em", fontFamily:"'Outfit',sans-serif" }}>INVENTRA</div>
            <div style={{ fontSize:9, color:"#4E525E", letterSpacing:".14em", fontWeight:600, fontFamily:"'IBM Plex Mono',monospace" }}>MGMT SYSTEM v1.0</div>
          </div>
        </div>

        <div style={{ fontSize:9, color:"#3A3D47", letterSpacing:".12em", fontWeight:700, fontFamily:"'IBM Plex Mono',monospace", padding:"0 14px", marginBottom:6 }}>NAVIGATION</div>

        <nav style={{ display:"flex", flexDirection:"column", gap:2, padding:"0 8px" }}>
          {navItems.map(n=>(
            <button key={n.id} onClick={()=>{setView(n.id);setSearch("");setCatFilter("All");}} style={{ ...S.navBtn, ...(view===n.id?S.navActive:{}) }}>
              <span style={{ display:"flex", alignItems:"center", justifyContent:"center", width:32, height:32, borderRadius:8, background:view===n.id?"#D4820E18":"transparent", color:view===n.id?"#D4820E":"#6B6F7A", transition:"all .2s" }}>{n.icon}</span>
              {n.label}
            </button>
          ))}
        </nav>

        <div style={{ marginTop:"auto", padding:"16px 14px", borderTop:"1px solid #22242C" }}>
          <div style={{ display:"flex", alignItems:"center", gap:10 }}>
            <div style={{ width:8, height:8, borderRadius:"50%", background:"#2E9B63", boxShadow:"0 0 8px #2E9B6366", animation:"pulse 2s infinite" }}/>
            <span style={{ fontSize:11, color:"#4E525E", fontFamily:"'IBM Plex Mono',monospace" }}>SYSTEM ONLINE</span>
          </div>
        </div>
      </aside>

      {/* ═══ MAIN CONTENT ═══ */}
      <main style={S.main}>
        {/* ─── DASHBOARD ─── */}
        {view==="dashboard" && (
          <div style={{ animation:"fadeIn .35s ease" }}>
            <div style={{ marginBottom:28 }}>
              <h1 style={S.pageTitle}>Dashboard</h1>
              <p style={{ color:"#4E525E", fontSize:13, margin:"6px 0 0", fontFamily:"'IBM Plex Mono',monospace" }}>Real-time inventory overview</p>
            </div>

            <div style={{ display:"grid", gridTemplateColumns:"repeat(auto-fill,minmax(210px,1fr))", gap:14, marginBottom:32 }}>
              <StatTile icon={<svg width="32" height="32" fill="none" stroke="currentColor" strokeWidth="1.5" viewBox="0 0 24 24"><path d="M21 16V8a2 2 0 00-1-1.73l-7-4a2 2 0 00-2 0l-7 4A2 2 0 002 8v8a2 2 0 001 1.73l7 4a2 2 0 002 0l7-4A2 2 0 0021 16z"/></svg>} label="Total Products" value={products.length} sub={`${rawCount} raw materials · ${finCount} finished`} accent="#D4820E" />
              <StatTile icon={I.dollar} label="Inventory Value" value={`$${totalVal.toLocaleString("en",{minimumFractionDigits:2})}`} accent="#3A8FD6" />
              <StatTile icon={I.alert} label="Low Stock" value={lowStock.length} sub={lowStock.length?`${lowStock.length} item${lowStock.length>1?"s":""} below reorder level`:"All items sufficiently stocked"} accent={lowStock.length?"#CF4040":"#2E9B63"} />
              <StatTile icon={I.shield} label="Active Accounts" value={users.length} sub={`${users.filter(u=>u.privilege==="Administrator").length} admins · ${users.filter(u=>u.privilege==="Staff").length} staff`} accent="#8B6FC0" />
            </div>

            {/* Low stock alerts */}
            {lowStock.length > 0 && (
              <div style={{ marginBottom:32 }}>
                <div style={{ display:"flex", alignItems:"center", gap:8, marginBottom:16 }}>
                  <span style={{ color:"#CF4040" }}>{I.alert}</span>
                  <h2 style={{ ...S.sectionTitle, margin:0 }}>Low Stock Alerts</h2>
                </div>
                <div style={{ display:"grid", gridTemplateColumns:"repeat(auto-fill,minmax(260px,1fr))", gap:12 }}>
                  {lowStock.map((p,i)=>{
                    const pct = Math.min(100, p.reorderLevel>0 ? (p.quantity/p.reorderLevel)*100 : 0);
                    const critical = pct < 50;
                    return (
                      <div key={i} style={{ background:"#1C1E24", borderRadius:10, padding:"16px 20px", border:`1px solid ${critical?"#CF404030":"#2A2D35"}`, position:"relative", overflow:"hidden" }}>
                        <div style={{ display:"flex", justifyContent:"space-between", alignItems:"flex-start", marginBottom:8 }}>
                          <div style={{ fontWeight:600, color:"#EAECF0", fontSize:14 }}>{p.name}</div>
                          <Badge text={critical?"CRITICAL":"LOW"} tone="low" />
                        </div>
                        <div style={{ display:"flex", justifyContent:"space-between", fontSize:12, color:"#6B6F7A", marginBottom:10, fontFamily:"'IBM Plex Mono',monospace" }}>
                          <span>Qty: <strong style={{color:critical?"#CF4040":"#D4820E"}}>{p.quantity}</strong></span>
                          <span>Reorder at: {p.reorderLevel}</span>
                        </div>
                        <div style={{ height:4, borderRadius:2, background:"#2A2D35" }}>
                          <div style={{ height:4, borderRadius:2, width:`${pct}%`, background:critical?"#CF4040":"#D4820E", transition:"width .5s" }}/>
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
            )}

            {/* Category breakdown */}
            <div>
              <h2 style={{ ...S.sectionTitle, marginBottom:16 }}>Category Breakdown</h2>
              <div style={{ display:"grid", gridTemplateColumns:"repeat(auto-fill,minmax(280px,1fr))", gap:14 }}>
                {[{cat:"RawMaterial",label:"Raw Materials",tone:"raw",accent:"#D4820E"},{cat:"Finished",label:"Finished Goods",tone:"fin",accent:"#3A8FD6"}].map(c=>{
                  const items = products.filter(p=>p.category===c.cat);
                  const val = items.reduce((s,p)=>s+p.price*p.quantity,0);
                  const totalQty = items.reduce((s,p)=>s+p.quantity,0);
                  return (
                    <div key={c.cat} style={{ background:"#1C1E24", borderRadius:10, padding:"22px 24px", border:"1px solid #2A2D35" }}>
                      <div style={{ display:"flex", justifyContent:"space-between", alignItems:"center", marginBottom:16 }}>
                        <Badge text={c.label} tone={c.tone} />
                        <span style={{ fontSize:12, color:"#4E525E", fontFamily:"'IBM Plex Mono',monospace" }}>{items.length} SKU{items.length!==1?"s":""}</span>
                      </div>
                      <div style={{ display:"flex", gap:24 }}>
                        <div>
                          <div style={{ fontSize:24, fontWeight:800, color:"#EAECF0", fontFamily:"'Outfit',sans-serif" }}>${val.toLocaleString("en",{minimumFractionDigits:2})}</div>
                          <div style={{ fontSize:11, color:"#4E525E", marginTop:2, fontFamily:"'IBM Plex Mono',monospace" }}>TOTAL VALUE</div>
                        </div>
                        <div style={{ borderLeft:"1px solid #2A2D35", paddingLeft:24 }}>
                          <div style={{ fontSize:24, fontWeight:800, color:"#EAECF0", fontFamily:"'Outfit',sans-serif" }}>{totalQty.toLocaleString()}</div>
                          <div style={{ fontSize:11, color:"#4E525E", marginTop:2, fontFamily:"'IBM Plex Mono',monospace" }}>TOTAL UNITS</div>
                        </div>
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>
          </div>
        )}

        {/* ─── PRODUCTS ─── */}
        {view==="products" && (
          <div style={{ animation:"fadeIn .35s ease" }}>
            <div style={{ display:"flex", justifyContent:"space-between", alignItems:"center", marginBottom:24, flexWrap:"wrap", gap:12 }}>
              <div>
                <h1 style={S.pageTitle}>Products</h1>
                <p style={{ color:"#4E525E", fontSize:13, margin:"6px 0 0", fontFamily:"'IBM Plex Mono',monospace" }}>{products.length} items across {rawCount} raw / {finCount} finished</p>
              </div>
              <button style={S.primaryBtn} onClick={openAddProd}>{I.plus} Add Product</button>
            </div>

            {/* Toolbar */}
            <div style={{ display:"flex", gap:12, marginBottom:18, flexWrap:"wrap", alignItems:"center" }}>
              <div style={S.searchWrap}>
                <span style={{ color:"#4E525E" }}>{I.search}</span>
                <input style={S.searchInput} placeholder="Search name, SKU, manufacturer…" value={search} onChange={e=>setSearch(e.target.value)} />
                {search && <button onClick={()=>setSearch("")} style={{ background:"none", border:"none", color:"#6B6F7A", cursor:"pointer", padding:2 }}>{I.x}</button>}
              </div>
              <div style={{ display:"flex", gap:3 }}>
                {["All","RawMaterial","Finished"].map(c=>(
                  <button key={c} onClick={()=>setCatFilter(c)} style={{ ...S.filterBtn, ...(catFilter===c?S.filterActive:{}) }}>
                    {c==="All"?"All":c==="RawMaterial"?"Raw":"Finished"}
                  </button>
                ))}
              </div>
            </div>

            {/* Table */}
            <div style={S.tableWrap}>
              <table style={{ width:"100%", borderCollapse:"collapse" }}>
                <thead>
                  <tr>
                    <SortTH col="name">Product</SortTH>
                    <th style={S.th}>SKU</th>
                    <SortTH col="category">Category</SortTH>
                    <SortTH col="manufacturer">Manufacturer</SortTH>
                    <SortTH col="price">Price</SortTH>
                    <SortTH col="quantity">Qty</SortTH>
                    <th style={S.th}>Reorder</th>
                    <th style={S.th}>Status</th>
                    <th style={{ ...S.th, textAlign:"right" }}>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {filtered.length===0 && (
                    <tr><td colSpan={9} style={{ padding:48, textAlign:"center", color:"#4E525E", fontFamily:"'IBM Plex Mono',monospace", fontSize:13 }}>No products match your search.</td></tr>
                  )}
                  {filtered.map(p=>(
                    <tr key={p.sku}>
                      <td style={S.td}><span style={{ fontWeight:600, color:"#EAECF0" }}>{p.name}</span></td>
                      <td style={S.td}><code style={S.skuCode}>{p.sku}</code></td>
                      <td style={S.td}><Badge text={p.category==="RawMaterial"?"Raw":"Finished"} tone={p.category==="RawMaterial"?"raw":"fin"} /></td>
                      <td style={S.td}><span style={{ color: p.manufacturer==="SELF"?"#D4820E":"#9B9EA6", fontFamily:"'IBM Plex Mono',monospace", fontSize:12 }}>{p.manufacturer}</span></td>
                      <td style={S.td}>${p.price.toFixed(2)}</td>
                      <td style={S.td}><span style={{ fontWeight:600 }}>{p.quantity.toLocaleString()}</span></td>
                      <td style={S.td}>{p.reorderLevel.toLocaleString()}</td>
                      <td style={S.td}><Badge text={p.quantity<=p.reorderLevel?"Low Stock":"In Stock"} tone={p.quantity<=p.reorderLevel?"low":"ok"} /></td>
                      <td style={{ ...S.td, textAlign:"right" }}>
                        <button style={S.actionBtn} onClick={()=>openEditProd(p._i)} title="Edit">{I.edit}</button>
                        <button style={{ ...S.actionBtn, color:"#CF4040" }} onClick={()=>confirmDeleteProd(p._i)} title="Delete">{I.trash}</button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* ─── ACCOUNTS ─── */}
        {view==="users" && (
          <div style={{ animation:"fadeIn .35s ease" }}>
            <div style={{ display:"flex", justifyContent:"space-between", alignItems:"center", marginBottom:28, flexWrap:"wrap", gap:12 }}>
              <div>
                <h1 style={S.pageTitle}>Accounts</h1>
                <p style={{ color:"#4E525E", fontSize:13, margin:"6px 0 0", fontFamily:"'IBM Plex Mono',monospace" }}>{users.length} registered users</p>
              </div>
              <button style={S.primaryBtn} onClick={openAddUser}>{I.plus} Add User</button>
            </div>

            <div style={{ display:"grid", gridTemplateColumns:"repeat(auto-fill,minmax(340px,1fr))", gap:14 }}>
              {users.map((u,i)=>{
                const uname = genUsername(u.firstName,u.lastName);
                const cols = { Administrator:"#D4820E", Staff:"#3A8FD6", Guest:"#6B6F7A" };
                const col = cols[u.privilege];
                return (
                  <div key={i} style={{ background:"#1C1E24", borderRadius:10, padding:"20px 22px", border:"1px solid #2A2D35", display:"flex", alignItems:"center", gap:16, position:"relative", overflow:"hidden" }}>
                    <div style={{ width:52, height:52, borderRadius:12, background:"#15171D", display:"flex", alignItems:"center", justifyContent:"center", border:`1.5px solid ${col}40`, flexShrink:0 }}>
                      <span style={{ fontSize:18, fontWeight:800, color:col, fontFamily:"'Outfit',sans-serif" }}>{u.firstName[0]}{u.lastName[0]}</span>
                    </div>
                    <div style={{ flex:1, minWidth:0 }}>
                      <div style={{ fontWeight:700, color:"#EAECF0", fontSize:15, fontFamily:"'Outfit',sans-serif" }}>{u.firstName} {u.lastName}</div>
                      <div style={{ fontSize:12, color:"#4E525E", fontFamily:"'IBM Plex Mono',monospace", marginTop:2 }}>@{uname}</div>
                      <div style={{ marginTop:8 }}><Badge text={u.privilege} tone={u.privilege.toLowerCase()} /></div>
                    </div>
                    <div style={{ display:"flex", flexDirection:"column", gap:6 }}>
                      <button style={S.actionBtn} onClick={()=>openEditUser(i)} title="Edit">{I.edit}</button>
                      <button style={{ ...S.actionBtn, color:"#CF4040" }} onClick={()=>confirmDeleteUser(i)} title="Delete">{I.trash}</button>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        )}
      </main>

      {/* ═══ PRODUCT MODAL ═══ */}
      <Modal open={prodModal!==null} onClose={()=>setProdModal(null)} title={prodModal==="add"?"Add Product":"Edit Product"}>
        <div style={S.formGrid}>
          <label style={S.field}>
            <span style={S.label}>Product Name *</span>
            <input style={S.input} value={pForm.name} onChange={e=>setPForm({...pForm,name:e.target.value})} placeholder="e.g. Cold-Rolled Steel Sheet" />
          </label>
          <label style={S.field}>
            <span style={S.label}>Category</span>
            <select style={S.input} value={pForm.category} onChange={e=>setPForm({...pForm,category:e.target.value})}>
              <option value="RawMaterial">Raw Material</option>
              <option value="Finished">Finished</option>
            </select>
          </label>
          <label style={S.field}>
            <span style={S.label}>Price ($)</span>
            <input style={S.input} type="number" step="0.01" min="0" value={pForm.price} onChange={e=>setPForm({...pForm,price:e.target.value})} placeholder="0.00" />
          </label>
          <label style={S.field}>
            <span style={S.label}>Quantity</span>
            <input style={S.input} type="number" min="0" value={pForm.quantity} onChange={e=>setPForm({...pForm,quantity:e.target.value})} placeholder="0" />
          </label>
          <label style={S.field}>
            <span style={S.label}>Reorder Level</span>
            <input style={S.input} type="number" min="0" value={pForm.reorderLevel} onChange={e=>setPForm({...pForm,reorderLevel:e.target.value})} placeholder="0" />
          </label>
          <label style={S.field}>
            <span style={S.label}>Manufacturer</span>
            <input style={S.input} value={pForm.manufacturer} onChange={e=>setPForm({...pForm,manufacturer:e.target.value})} placeholder="Leave blank for SELF" />
          </label>
        </div>
        <div style={S.modalFooter}>
          <button style={S.ghostBtn} onClick={()=>setProdModal(null)}>Cancel</button>
          <button style={S.primaryBtn} onClick={saveProd}>{prodModal==="add"?"Add Product":"Save Changes"}</button>
        </div>
      </Modal>

      {/* ═══ USER MODAL ═══ */}
      <Modal open={userModal!==null} onClose={()=>setUserModal(null)} title={userModal==="add"?"Add User":"Edit User"}>
        <div style={S.formGrid}>
          <label style={S.field}>
            <span style={S.label}>First Name *</span>
            <input style={S.input} value={uForm.firstName} onChange={e=>setUForm({...uForm,firstName:e.target.value})} placeholder="Diana" />
          </label>
          <label style={S.field}>
            <span style={S.label}>Last Name *</span>
            <input style={S.input} value={uForm.lastName} onChange={e=>setUForm({...uForm,lastName:e.target.value})} placeholder="Kovacs" />
          </label>
          <label style={S.field}>
            <span style={S.label}>Privilege Level</span>
            <select style={S.input} value={uForm.privilege} onChange={e=>setUForm({...uForm,privilege:e.target.value})}>
              <option value="Administrator">Administrator</option>
              <option value="Staff">Staff</option>
              <option value="Guest">Guest</option>
            </select>
          </label>
        </div>
        {uForm.firstName && uForm.lastName && (
          <div style={{ marginTop:16, padding:"12px 16px", background:"#15171D", borderRadius:8, border:"1px solid #2A2D35" }}>
            <span style={{ fontSize:12, color:"#4E525E", fontFamily:"'IBM Plex Mono',monospace" }}>AUTO-GENERATED USERNAME: </span>
            <strong style={{ color:"#D4820E", fontFamily:"'IBM Plex Mono',monospace", fontSize:13 }}>@{genUsername(uForm.firstName,uForm.lastName)}</strong>
          </div>
        )}
        <div style={S.modalFooter}>
          <button style={S.ghostBtn} onClick={()=>setUserModal(null)}>Cancel</button>
          <button style={S.primaryBtn} onClick={saveUser}>{userModal==="add"?"Create User":"Save Changes"}</button>
        </div>
      </Modal>

      {/* ═══ CONFIRM ═══ */}
      <ConfirmDialog open={!!confirm} message={confirm?.message} onConfirm={confirm?.action} onCancel={()=>setConfirm(null)} />

      {/* ═══ TOAST ═══ */}
      {toast && (
        <div style={S.toast}>{toast}</div>
      )}
    </div>
  );
}

/* ─── Style constants ─── */
const S = {
  root: { display:"flex", height:"100vh", fontFamily:"'Outfit',sans-serif", background:"#13151B", color:"#9B9EA6", fontSize:14, overflow:"hidden" },
  sidebar: { width:232, minWidth:232, background:"#181A20", borderRight:"1px solid #22242C", display:"flex", flexDirection:"column", overflow:"auto" },
  brand: { display:"flex", alignItems:"center", gap:12, padding:"24px 18px 28px" },
  brandMark: { width:42, height:42, borderRadius:11, background:"#D4820E10", border:"1px solid #D4820E25", display:"flex", alignItems:"center", justifyContent:"center", flexShrink:0 },
  navBtn: { display:"flex", alignItems:"center", gap:10, padding:"6px 8px", border:"none", background:"transparent", color:"#6B6F7A", fontSize:14, fontWeight:500, fontFamily:"'Outfit',sans-serif", borderRadius:10, cursor:"pointer", transition:"all .15s", textAlign:"left", width:"100%" },
  navActive: { color:"#EAECF0", fontWeight:600 },
  main: { flex:1, overflow:"auto", padding:"32px 36px" },
  pageTitle: { fontSize:26, fontWeight:800, color:"#EAECF0", margin:0, letterSpacing:"-.03em", fontFamily:"'Outfit',sans-serif" },
  sectionTitle: { fontSize:15, fontWeight:700, color:"#EAECF0", fontFamily:"'Outfit',sans-serif" },
  primaryBtn: { display:"inline-flex", alignItems:"center", gap:7, padding:"10px 20px", background:"#D4820E", color:"#13151B", border:"none", borderRadius:8, fontSize:13, fontWeight:700, fontFamily:"'Outfit',sans-serif", cursor:"pointer", letterSpacing:".01em" },
  ghostBtn: { padding:"10px 20px", background:"transparent", color:"#6B6F7A", border:"1px solid #2A2D35", borderRadius:8, fontSize:13, fontWeight:600, fontFamily:"'Outfit',sans-serif", cursor:"pointer" },
  searchWrap: { display:"flex", alignItems:"center", gap:8, background:"#1C1E24", border:"1px solid #2A2D35", borderRadius:8, padding:"8px 14px", flex:"1 1 280px", maxWidth:420 },
  searchInput: { border:"none", background:"transparent", color:"#EAECF0", fontSize:13, fontFamily:"'Outfit',sans-serif", outline:"none", width:"100%" },
  filterBtn: { padding:"7px 16px", border:"1px solid #2A2D35", background:"transparent", color:"#6B6F7A", fontSize:12, fontWeight:600, fontFamily:"'IBM Plex Mono',monospace", borderRadius:6, cursor:"pointer", letterSpacing:".03em", transition:"all .15s" },
  filterActive: { background:"#D4820E18", color:"#D4820E", borderColor:"#D4820E40" },
  tableWrap: { background:"#1C1E24", borderRadius:10, border:"1px solid #2A2D35", overflow:"auto" },
  th: { textAlign:"left", padding:"14px 16px", fontSize:10, fontWeight:700, color:"#4E525E", textTransform:"uppercase", letterSpacing:".1em", borderBottom:"1px solid #2A2D35", whiteSpace:"nowrap", fontFamily:"'IBM Plex Mono',monospace", background:"#181A20" },
  td: { padding:"14px 16px", fontSize:13, whiteSpace:"nowrap", borderBottom:"1px solid #1E2028", transition:"background .1s" },
  skuCode: { fontSize:11, fontFamily:"'IBM Plex Mono',monospace", background:"#15171D", padding:"3px 8px", borderRadius:4, color:"#6B6F7A", border:"1px solid #22242C" },
  actionBtn: { background:"none", border:"none", color:"#6B6F7A", cursor:"pointer", padding:7, borderRadius:6, display:"inline-flex", alignItems:"center", justifyContent:"center" },
  formGrid: { display:"grid", gridTemplateColumns:"1fr 1fr", gap:16 },
  field: { display:"flex", flexDirection:"column", gap:6 },
  label: { fontSize:10, fontWeight:700, color:"#4E525E", textTransform:"uppercase", letterSpacing:".1em", fontFamily:"'IBM Plex Mono',monospace" },
  input: { padding:"10px 14px", background:"#13151B", border:"1px solid #2A2D35", borderRadius:8, color:"#EAECF0", fontSize:13, fontFamily:"'Outfit',sans-serif", transition:"border-color .2s" },
  modalFooter: { display:"flex", justifyContent:"flex-end", gap:10, marginTop:24, paddingTop:18, borderTop:"1px solid #22242C" },
  toast: { position:"fixed", bottom:28, left:"50%", transform:"translateX(-50%)", background:"#EAECF0", color:"#13151B", padding:"11px 24px", borderRadius:8, fontSize:13, fontWeight:700, fontFamily:"'Outfit',sans-serif", boxShadow:"0 12px 40px rgba(0,0,0,.45)", zIndex:2000, animation:"slideToast .3s ease", whiteSpace:"nowrap" },
};
