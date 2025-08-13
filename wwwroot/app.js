const $ = (q) => document.querySelector(q);
const listEl = $("#list");
const form = $("#shortenForm");
const urlInput = $("#url");
const codeInput = $("#code");
const result = $("#result");
const shortLinkEl = $("#shortLink");
const resultLong = $("#resultLong");
const copyBtn = $("#copyBtn");
const openBtn = $("#openBtn");
const refreshBtn = $("#refreshBtn");
const toast = $("#toast");

function showToast(msg){
  toast.textContent = msg;
  toast.classList.add("show");
  setTimeout(()=> toast.classList.remove("show"), 1600);
}

function setLoading(on){
  if(on){ form.classList.add("loading"); }
  else { form.classList.remove("loading"); }
}

async function apiShorten(url, code){
  const r = await fetch("/shorten", {
    method: "POST",
    headers: {"Content-Type": "application/json"},
    body: JSON.stringify({ url, code: code || undefined })
  });
  const data = await r.json().catch(()=> ({}));
  if(!r.ok) throw new Error(data.error || "Request failed");
  return data;
}

async function apiList(){
  const r = await fetch("/api/links");
  if(!r.ok) return [];
  return r.json();
}

function validate(){
  const url = urlInput.value.trim();
  if(!url || !/^https?:\/\//i.test(url)){ urlInput.focus(); throw new Error("Enter a valid http/https URL"); }
  const code = codeInput.value.trim();
  if(code && !/^[A-Za-z0-9_-]{4,20}$/.test(code)){
    codeInput.focus();
    throw new Error("Code must be 4-20 chars [A-Za-z0-9_-]");
  }
  return { url, code };
}

form.addEventListener("submit", async (e)=>{
  e.preventDefault();
  try{
    const { url, code } = validate();
    setLoading(true);
    const data = await apiShorten(url, code);
    shortLinkEl.href = data.shortUrl;
    shortLinkEl.textContent = data.shortUrl;
    resultLong.textContent = data.url;
    result.classList.remove("hidden");
    showToast("Short link created");
    urlInput.value = ""; codeInput.value = "";
    await refresh();
  }catch(err){
    showToast(err.message || "Error");
  }finally{
    setLoading(false);
  }
});

copyBtn.addEventListener("click", async ()=>{
  const text = shortLinkEl.href;
  await navigator.clipboard.writeText(text);
  showToast("Copied to clipboard");
});

openBtn.addEventListener("click", ()=>{
  window.open(shortLinkEl.href, "_blank", "noopener");
});

refreshBtn.addEventListener("click", refresh);

async function refresh(){
  const host = location.origin;
  const items = await apiList();
  if(!items.length){
    listEl.classList.add("empty");
    listEl.innerHTML = `<div class="empty-state"><div class="logo-sm">US</div><p>No links yet. Create your first short link above.</p></div>`;
    return;
  }
  listEl.classList.remove("empty");
  listEl.innerHTML = items.map(x => {
    const short = `${host}/${x.code}`;
    const created = new Date(x.createdAt);
    return `
      <div class="item">
        <div class="left">
          <div class="short"><a href="${short}" target="_blank" rel="noopener">${short}</a></div>
          <div class="long">${x.url}</div>
          <div class="badge">${created.toLocaleString()}</div>
        </div>
        <div class="actions">
          <div class="kpis">
            <div class="k"><span class="dot"></span><span>${x.hits} hits</span></div>
          </div>
          <button class="btn ghost" onclick="navigator.clipboard.writeText('${short}').then(()=>toast.classList.add('show'),()=>0); setTimeout(()=>toast.classList.remove('show'),1600)">Copy</button>
          <a class="btn" href="${short}" target="_blank" rel="noopener">Open</a>
        </div>
      </div>`;
  }).join("");
}

refresh();
