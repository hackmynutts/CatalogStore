using System.Text.Json;

namespace ImportImages;

// Página HTML local para revisar las asignaciones viendo cada foto. Se abre con doble clic (sin servidor):
// las imágenes se leen de la carpeta original por ruta relativa y los cambios se guardan en el navegador.
// Al terminar, "Descargar CSV" genera imagenes_revision.csv con el mismo formato que lee el comando upload.
public static class ReviewPage
{
    public static void Write(string path, string imagesRelativeDir, List<ReviewRow> rows, List<Product> products)
    {
        var data = new
        {
            imagesDir = imagesRelativeDir.Replace('\\', '/'),
            rows = rows.Select(r => new
            {
                file = r.File,
                action = r.Action,
                match = r.Match,
                ids = r.ProductIds,
                candidates = r.Candidates.Select(c => new { ids = c.Ids, name = c.Name, score = Math.Round(c.Score, 2), diff = c.Differences }),
                note = r.Note
            }),
            products = products.OrderBy(p => p.Name).Select(p => new { id = p.Id, code = p.Code, name = p.Name.Trim(), hasImage = p.HasImage })
        };
        // El serializador por defecto escapa '<' y '>' (<), así el JSON no puede cerrar el <script>.
        var json = JsonSerializer.Serialize(data);
        File.WriteAllText(path, Template.Replace("__DATA__", json));
    }

    private const string Template = """
<!doctype html>
<html lang="es">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Revisión de imágenes</title>
<style>
  :root { --brand:#2f5d50; --brand-dark:#24473d; --brand-light:#e8f0ee; --accent:#c8a24a; --text:#1f2937; --muted:#6b7280;
          --border:#e5e7eb; --bg:#f5f6f8; --card:#fff; --danger:#dc3545; --danger-bg:#fdecea; --warn-bg:#fbf3df; --warn:#7a5a12; --ok-bg:#dcfce7; --ok:#166534; }
  * { box-sizing:border-box; }
  body { margin:0; font-family:"Segoe UI", system-ui, sans-serif; background:var(--bg); color:var(--text); }
  header { position:sticky; top:0; z-index:10; background:linear-gradient(90deg,var(--brand-dark),var(--brand)); color:#fff; padding:12px 20px; box-shadow:0 2px 12px rgba(0,0,0,.18); }
  header h1 { margin:0 0 8px; font-size:1.15rem; }
  .bar { display:flex; flex-wrap:wrap; gap:8px; align-items:center; }
  .pill { border:1px solid rgba(255,255,255,.4); background:transparent; color:#fff; border-radius:999px; padding:6px 14px; font-weight:600; font-size:.82rem; cursor:pointer; }
  .pill.active { background:#fff; color:var(--brand-dark); }
  .bar input[type=search] { border:none; border-radius:8px; padding:8px 12px; min-width:240px; font-size:.9rem; }
  .bar .spacer { flex:1; }
  .btn { border:none; border-radius:8px; padding:8px 14px; font-weight:600; cursor:pointer; font-size:.88rem; }
  .btn-primary { background:var(--accent); color:#1f2937; }
  .btn-light { background:rgba(255,255,255,.15); color:#fff; }
  main { padding:20px; max-width:1300px; margin:0 auto; }
  .help { background:var(--card); border-radius:12px; padding:14px 18px; margin-bottom:16px; font-size:.88rem; color:var(--muted); box-shadow:0 4px 14px rgba(15,23,42,.06); }
  .help b { color:var(--text); }
  .grid { display:grid; grid-template-columns:repeat(auto-fill, minmax(560px, 1fr)); gap:16px; }
  @media (max-width:620px) { .grid { grid-template-columns:1fr; } .card { grid-template-columns:1fr !important; } }
  .card { background:var(--card); border-radius:14px; box-shadow:0 6px 20px rgba(15,23,42,.07); display:grid; grid-template-columns:190px 1fr; overflow:hidden; border-left:5px solid var(--border); }
  .card[data-action=SUBIR] { border-left-color:var(--brand); }
  .card[data-action=REVISAR] { border-left-color:var(--accent); }
  .card[data-action=SIN_COINCIDENCIA] { border-left-color:var(--danger); }
  .card[data-action=OMITIR], .card[data-action=YA_TIENE_IMAGEN] { border-left-color:#9ca3af; opacity:.8; }
  .thumb { background:#f6f7f9; display:flex; align-items:center; justify-content:center; padding:8px; cursor:zoom-in; }
  .thumb img { max-width:100%; max-height:190px; object-fit:contain; }
  .body { padding:12px 14px; display:flex; flex-direction:column; gap:8px; min-width:0; }
  .file { font-weight:700; font-size:.92rem; word-break:break-word; }
  .match { font-size:.75rem; color:var(--muted); }
  .row { display:flex; gap:8px; align-items:center; flex-wrap:wrap; }
  select, input.ids, input.add { border:1px solid var(--border); border-radius:6px; padding:6px 8px; font-size:.85rem; font-family:inherit; }
  input.ids { width:130px; }
  input.add { flex:1; min-width:180px; }
  .chips { display:flex; flex-direction:column; gap:4px; }
  .chip { display:flex; align-items:center; gap:6px; background:var(--brand-light); color:var(--brand-dark); border-radius:6px; padding:4px 8px; font-size:.8rem; }
  .chip .x { margin-left:auto; border:none; background:transparent; cursor:pointer; color:var(--muted); font-size:1rem; line-height:1; }
  .chip.bad { background:var(--danger-bg); color:#9b1c1c; }
  .tag { font-size:.68rem; font-weight:700; border-radius:999px; padding:1px 7px; background:#f1f1f2; color:var(--muted); }
  .tag.has { background:var(--warn-bg); color:var(--warn); }
  .cands { display:flex; flex-direction:column; gap:4px; }
  .cand { display:flex; align-items:center; gap:6px; font-size:.78rem; color:var(--muted); }
  .cand button { border:1px solid var(--border); background:#fff; border-radius:6px; padding:2px 8px; cursor:pointer; font-size:.75rem; }
  .cand button:hover { border-color:var(--brand); color:var(--brand-dark); }
  .note { font-size:.75rem; color:var(--warn); background:var(--warn-bg); border-radius:6px; padding:4px 8px; }
  .label { font-size:.68rem; font-weight:700; text-transform:uppercase; letter-spacing:.4px; color:var(--muted); }
  .empty { text-align:center; color:var(--muted); padding:40px; }
  dialog { border:none; border-radius:12px; padding:0; max-width:90vw; max-height:90vh; }
  dialog img { max-width:90vw; max-height:85vh; display:block; }
  dialog::backdrop { background:rgba(0,0,0,.7); }
</style>
</head>
<body>
<header>
  <h1>Revisión de imágenes del catálogo <span id="counts" style="font-weight:400;font-size:.85rem;opacity:.9"></span></h1>
  <div class="bar">
    <button class="pill active" data-filter="ALL">Todas</button>
    <button class="pill" data-filter="REVISAR">Revisar</button>
    <button class="pill" data-filter="SIN_COINCIDENCIA">Sin coincidencia</button>
    <button class="pill" data-filter="SUBIR">Subir</button>
    <button class="pill" data-filter="OMITIR">Omitir</button>
    <button class="pill" data-filter="YA_TIENE_IMAGEN">Ya tiene imagen</button>
    <input type="search" id="search" placeholder="Buscar por archivo o producto…" aria-label="Buscar">
    <span class="spacer"></span>
    <button class="btn btn-light" id="reset" title="Borra los cambios guardados en este navegador">Restablecer</button>
    <button class="btn btn-primary" id="export">Descargar CSV</button>
  </div>
</header>
<main>
  <div class="help">
    <b>Cómo revisar:</b> mirá la foto y el producto asignado. Si está bien, poné la acción en <b>SUBIR</b>.
    Si no, usá un candidato (<b>Usar</b> reemplaza, <b>+</b> agrega) o buscá el producto en el campo de búsqueda.
    Muchas fotos traen impreso el <b>código</b> del producto: compará con la etiqueta <b>Cód.</b> (también podés buscar por código).
    Una foto puede ir a varios productos (tallas o colores). Las fotos que no sirven: <b>OMITIR</b>.
    Los cambios se guardan solos en este navegador. Al terminar: <b>Descargar CSV</b> y guardalo como <b>imagenes_revision.csv</b> en esta carpeta.
  </div>
  <div class="grid" id="grid"></div>
  <div class="empty" id="empty" hidden>No hay archivos con este filtro.</div>
</main>
<datalist id="productList"></datalist>
<dialog id="zoom"><img id="zoomImg" alt=""></dialog>
<script>
const DATA = __DATA__;
const STORE = 'cs-img-review:';
const products = new Map(DATA.products.map(p => [p.id, p]));
const state = DATA.rows.map(r => {
  let saved = null;
  try { saved = JSON.parse(localStorage.getItem(STORE + r.file) || 'null'); } catch (e) {}
  return { ...r, action: saved?.action ?? r.action, ids: saved?.ids ?? r.ids.slice(), original: { action: r.action, ids: r.ids.slice() } };
});
let filter = 'ALL';

document.getElementById('productList').innerHTML =
  DATA.products.map(p => `<option value="${p.id} · ${esc(p.name)}${p.code ? ' (' + esc(p.code) + ')' : ''}"></option>`).join('');

function esc(v) { return String(v ?? '').replace(/[&<>"']/g, c => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;', "'":'&#39;' }[c])); }
function imgSrc(file) { return DATA.imagesDir.split('/').map(encodeURIComponent).join('/') + '/' + encodeURIComponent(file); }
function save(row) { try { localStorage.setItem(STORE + row.file, JSON.stringify({ action: row.action, ids: row.ids })); } catch (e) {} }

// Las fotos traen impreso el código del producto: mostrarlo permite comparar a simple vista.
function codes(ids) {
  const list = ids.map(id => products.get(id)?.code).filter(Boolean);
  return list.length ? `<span class="tag">Cód. ${esc(list.slice(0, 4).join(', '))}${list.length > 4 ? '…' : ''}</span>` : '';
}

function chipsHtml(row) {
  if (row.ids.length === 0) return '<span class="match">Sin productos asignados.</span>';
  return row.ids.map(id => {
    const p = products.get(id);
    if (!p) return `<div class="chip bad">ID ${id}: no existe o está inactivo <button class="x" data-remove="${id}" aria-label="Quitar">×</button></div>`;
    return `<div class="chip"><b>${id}</b> ${p.code ? `<span class="tag">Cód. ${esc(p.code)}</span>` : ''} ${esc(p.name)} ${p.hasImage ? '<span class="tag has">ya tiene foto</span>' : ''}
            <button class="x" data-remove="${id}" aria-label="Quitar ${esc(p.name)}">×</button></div>`;
  }).join('');
}

function cardHtml(row, i) {
  const cands = row.candidates.map((c, ci) => `
      <div class="cand"><button data-use="${ci}">Usar</button><button data-add="${ci}" title="Agregar">+</button>
      <span><b>${esc(c.ids.join(','))}</b> ${codes(c.ids)} ${esc(c.name)} · ${c.score} · ${esc(c.diff)}</span></div>`).join('');
  return `
  <article class="card" data-i="${i}" data-action="${row.action}">
    <div class="thumb" data-zoom><img loading="lazy" src="${imgSrc(row.file)}" alt="${esc(row.file)}"></div>
    <div class="body">
      <div class="file">${esc(row.file)}</div>
      <div class="match">${esc(row.match)}</div>
      <div class="row">
        <select data-field="action" aria-label="Acción">
          ${['SUBIR','REVISAR','SIN_COINCIDENCIA','OMITIR','YA_TIENE_IMAGEN'].map(a => `<option ${a === row.action ? 'selected' : ''}>${a}</option>`).join('')}
        </select>
        <input class="ids" data-field="ids" value="${row.ids.join(',')}" aria-label="IDs de producto" title="IDs separados por coma">
      </div>
      <div class="chips">${chipsHtml(row)}</div>
      <div class="row">
        <input class="add" list="productList" placeholder="Buscar producto para agregar…" aria-label="Buscar producto">
      </div>
      ${cands ? `<span class="label">Otros candidatos</span><div class="cands">${cands}</div>` : ''}
      ${row.note ? `<div class="note">${esc(row.note)}</div>` : ''}
    </div>
  </article>`;
}

function visible(row) {
  const term = document.getElementById('search').value.trim().toLowerCase();
  const byFilter = filter === 'ALL' || row.action === filter;
  if (!byFilter) return false;
  if (!term) return true;
  const names = row.ids.map(id => products.get(id)?.name || '').join(' ').toLowerCase();
  return row.file.toLowerCase().includes(term) || names.includes(term);
}

function render() {
  const grid = document.getElementById('grid');
  const html = state.map((r, i) => visible(r) ? cardHtml(r, i) : '').join('');
  grid.innerHTML = html;
  document.getElementById('empty').hidden = html.length > 0;
  const c = a => state.filter(r => r.action === a).length;
  document.getElementById('counts').textContent =
    `· ${state.length} archivos · subir ${c('SUBIR')} · revisar ${c('REVISAR')} · sin coincidencia ${c('SIN_COINCIDENCIA')} · omitir ${c('OMITIR')}`;
}

function rerenderCard(i) {
  const old = document.querySelector(`.card[data-i="${i}"]`);
  if (!old) return render();
  const tmp = document.createElement('div');
  tmp.innerHTML = cardHtml(state[i], i);
  old.replaceWith(tmp.firstElementChild);
  render.countsOnly();
}
render.countsOnly = () => {
  const c = a => state.filter(r => r.action === a).length;
  document.getElementById('counts').textContent =
    `· ${state.length} archivos · subir ${c('SUBIR')} · revisar ${c('REVISAR')} · sin coincidencia ${c('SIN_COINCIDENCIA')} · omitir ${c('OMITIR')}`;
};

function setIds(row, ids) {
  row.ids = [...new Set(ids.filter(n => Number.isInteger(n) && n > 0))];
  // Elegir productos a mano es una decisión: pasa a SUBIR salvo que se haya omitido.
  if (row.ids.length > 0 && (row.action === 'REVISAR' || row.action === 'SIN_COINCIDENCIA')) row.action = 'SUBIR';
  save(row);
}

const grid = document.getElementById('grid');
grid.addEventListener('click', e => {
  const card = e.target.closest('.card'); if (!card) return;
  const i = Number(card.dataset.i); const row = state[i];
  if (e.target.closest('[data-zoom]')) {
    document.getElementById('zoomImg').src = imgSrc(row.file);
    document.getElementById('zoom').showModal();
    return;
  }
  const rm = e.target.closest('[data-remove]');
  if (rm) { row.ids = row.ids.filter(id => id !== Number(rm.dataset.remove)); save(row); rerenderCard(i); return; }
  const use = e.target.closest('[data-use]');
  if (use) { setIds(row, row.candidates[Number(use.dataset.use)].ids.slice()); rerenderCard(i); return; }
  const add = e.target.closest('[data-add]');
  if (add) { setIds(row, row.ids.concat(row.candidates[Number(add.dataset.add)].ids)); rerenderCard(i); return; }
});
grid.addEventListener('change', e => {
  const card = e.target.closest('.card'); if (!card) return;
  const i = Number(card.dataset.i); const row = state[i];
  if (e.target.dataset.field === 'action') { row.action = e.target.value; save(row); rerenderCard(i); }
  else if (e.target.dataset.field === 'ids') { setIds(row, e.target.value.split(/[,\s]+/).map(Number)); rerenderCard(i); }
  else if (e.target.classList.contains('add')) {
    const id = parseInt(e.target.value, 10);
    if (products.has(id)) { setIds(row, row.ids.concat([id])); rerenderCard(i); }
  }
});
document.getElementById('zoom').addEventListener('click', e => e.currentTarget.close());

document.querySelectorAll('.pill').forEach(p => p.addEventListener('click', () => {
  document.querySelectorAll('.pill').forEach(x => x.classList.remove('active'));
  p.classList.add('active'); filter = p.dataset.filter; render();
}));
document.getElementById('search').addEventListener('input', render);

document.getElementById('reset').addEventListener('click', () => {
  if (!confirm('¿Borrar todos los cambios hechos en esta página?')) return;
  state.forEach(r => { r.action = r.original.action; r.ids = r.original.ids.slice(); try { localStorage.removeItem(STORE + r.file); } catch (e) {} });
  render();
});

document.getElementById('export').addEventListener('click', () => {
  const field = v => /[;,"\n\r]/.test(v) ? '"' + v.replace(/"/g, '""') + '"' : v;
  const header = ['Archivo','Accion','Coincidencia','ProductIDs','ProductosEnSistema','OtrosCandidatos','Nota'];
  const lines = [header.join(';')].concat(state.map(r => [
    r.file, r.action, r.match, r.ids.join(','),
    r.ids.map(id => products.get(id)?.name || ('ID ' + id + ' inexistente')).join(' / '),
    r.candidates.map(c => `${c.ids.join(',')} ${c.name} (${c.score}; ${c.diff})`).join(' | '),
    r.note
  ].map(v => field(String(v))).join(';')));
  const blob = new Blob(['﻿' + lines.join('\r\n')], { type: 'text/csv;charset=utf-8' });
  const a = document.createElement('a');
  a.href = URL.createObjectURL(blob); a.download = 'imagenes_revision.csv'; a.click();
  URL.revokeObjectURL(a.href);
});

render();
</script>
</body>
</html>
""";
}
