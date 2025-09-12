// Navbar appearance on scroll + active link
(function () {
  const nav = document.querySelector('.qvg-navbar');
  if (nav) {
    const onScroll = () => {
      if (window.scrollY > 24) nav.classList.add('scrolled');
      else nav.classList.remove('scrolled');
    };
    onScroll();
    window.addEventListener('scroll', onScroll);
  }

  // Mark current page active
  const here = location.pathname.toLowerCase();
  document.querySelectorAll('.qvg-nav-links .nav-link').forEach(a => {
    const href = a.getAttribute('href') || '';
    if (!href) return;
    const url = new URL(href, location.origin);
    if (url.pathname.toLowerCase() === here) a.classList.add('active');
  });
})();

// Populate Reciter select from /data/reciters.json (sorted by DisplayName)
(async function () {
  const sel = document.getElementById('reciter');
  if (!sel) return;

  const current = sel.getAttribute('data-current') || '';
  try {
    const resp = await fetch('/data/reciters.json', { cache: 'no-store' });
    if (!resp.ok) throw new Error('HTTP ' + resp.status);
    const json = await resp.json();

    const list = Array.isArray(json) ? json : (json.reciters || []);
    const items = list
      .map(r => ({
        code: r.Code ?? r.code,
        name: r.DisplayName ?? r.displayName ?? (r.Code ?? r.code ?? '')
      }))
      .filter(r => r.code && r.name)
      .sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }));

    sel.innerHTML = '';
    const ph = new Option('Choose a reciter…', '', true, false);
    ph.disabled = true; ph.hidden = true;
    sel.add(ph);

    for (const r of items) {
      const opt = new Option(r.name, r.code, false, r.code === current);
      sel.add(opt);
    }
    if (![...sel.options].some(o => o.selected)) sel.selectedIndex = 0;
  } catch (err) {
    sel.innerHTML = '';
    const errOpt = new Option('Failed to load reciters', '', true, false);
    errOpt.disabled = true;
    sel.add(errOpt);
    console.error('reciters.json load error:', err);
  }
})();

// Generator submit (keeps antiforgery + robust JSON handling)
(function () {
  const form = document.getElementById('generateForm');
  if (!form) return;

  const btn = document.getElementById('generateBtn');
  const spinner = document.getElementById('genSpinner');
  const genText = document.getElementById('genText');
  const preview = document.getElementById('previewVideo');
  const dl = document.getElementById('downloadBtn');

  function setBusy(busy) {
    [...form.elements].forEach(el => el.disabled = busy);
    if (busy) spinner.classList.remove('d-none');
    else spinner.classList.add('d-none');
  }

  function showToast() {
    const toastEl = document.getElementById('appToast');
    if (!toastEl) return;
    const toast = new bootstrap.Toast(toastEl, { delay: 3000 });
    toast.show();
  }

  form.addEventListener('submit', async (e) => {
    e.preventDefault();

    // capture BEFORE disabling (includes antiforgery token)
    const fd = new FormData(form);

    genText.textContent = 'Generating...';
    setBusy(true);

    try {
      const resp = await fetch(form.action || '/Video/Generate', {
        method: 'POST',
        headers: { 'X-Requested-With': 'XMLHttpRequest' },
        body: fd,
        credentials: 'same-origin'
      });

      const ct = resp.headers.get('content-type') || '';
      let data = null;
      if (ct.includes('application/json')) {
        data = await resp.json();
      } else {
        const text = await resp.text();
        throw new Error(text || `Server responded ${resp.status}`);
      }

      if (!resp.ok || !data.success) {
        throw new Error(data?.message || 'Failed to generate video.');
      }

      // success
      preview.src = data.previewUrl;
      preview.load();
      dl.href = data.downloadUrl;
      dl.classList.remove('disabled');

      showToast();
      //genText.textContent = 'Re-Generate';
    } catch (err) {
      alert((err && err.message) ? err.message : 'Error generating video.');
      //genText.textContent = 'Generate';
    } finally {
      setBusy(false);
      genText.textContent = 'Generate';
    }
  });
})();
