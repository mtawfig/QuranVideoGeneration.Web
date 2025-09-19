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
      const previewUrl  = data.previewUrl  ?? data.PreviewUrl  ?? '';
      const downloadUrl = data.downloadUrl ?? data.DownloadUrl ?? '';

      preview.src = previewUrl;
      preview.load();
      dl.href = downloadUrl;
      dl.classList.remove('disabled');

      showToast();
      //genText.textContent = 'Re-Generate';
      //qvgSaveGenerationFromForm(data);
      qvgSaveGenerationFromForm({ ...data, previewUrl, downloadUrl });
    } catch (err) {
      alert((err && err.message) ? err.message : 'Error generating video.');
      //genText.textContent = 'Generate';
    } finally {
      setBusy(false);
      genText.textContent = 'Generate';
    }
  });
})();

// ===== Generations: Save after successful Generate =====
(function(){
  const KEY = 'qvg.generations';

  function dd2(n){ return n < 10 ? '0'+n : ''+n; }
  function fmtNow(){
    const d = new Date();
    return `${dd2(d.getDate())}-${dd2(d.getMonth()+1)}-${d.getFullYear()} ${dd2(d.getHours())}:${dd2(d.getMinutes())}`;
  }

  function getSelectedText(sel){
    if (!sel || !sel.selectedOptions || !sel.selectedOptions[0]) return '';
    const t = sel.selectedOptions[0].textContent || '';
    // If "EN || AR" format, take EN part
    const parts = t.split('||');
    return parts[0].trim();
  }

  window.qvgSaveGenerationFromForm = function(result){
    try{
      const reciterSel = document.getElementById('reciter');
      const surahSel   = document.getElementById('surah');
      const styleSel   = document.getElementById('VideoStyle');
      const fromAyah   = document.getElementById('fromAyah');
      const toAyah     = document.getElementById('toAyah');

      const item = {
        ReciterName: getSelectedText(reciterSel),
        SurahName:   getSelectedText(surahSel).replace(/^\d+\s*—\s*/, ''), // drop index if present
        FromAyah:    parseInt(fromAyah?.value || '1', 10),
        ToAyah:      parseInt(toAyah?.value   || '1', 10),
        VideoStyleName: getSelectedText(styleSel),
        //VideoURL:    (result?.DownloadUrl || result?.PreviewUrl || '').toString(),
        VideoURL: (
          result?.downloadUrl ??
          result?.DownloadUrl ??
          result?.previewUrl  ??
          result?.PreviewUrl  ??
          ''
        ).toString(),
        CreateDateTime: fmtNow(),
        ts: Date.now()
      };

      if (!item.VideoURL) return;

      const raw = localStorage.getItem(KEY);
      let arr = [];
      try { arr = raw ? JSON.parse(raw) : []; } catch {}
      // de-dup by VideoURL
      arr = arr.filter(x => x.VideoURL !== item.VideoURL);
      arr.unshift(item);
      // keep last 100 max
      if (arr.length > 100) arr = arr.slice(0, 100);
      localStorage.setItem(KEY, JSON.stringify(arr));
    } catch(e) {
      // non-fatal
      console.warn('qvgSaveGenerationFromForm failed:', e);
    }
  };
})();
// ===== End Generations =====


// ===== Feedback form (Contact section) + Turnstile (invisible) =====
(function () {
  const form    = document.getElementById('feedbackForm');
  if (!form) return;

  const btn     = document.getElementById('sendFeedbackBtn');
  const spinner = document.getElementById('fbSpinner');

  const toastEl  = document.getElementById('feedbackToast');
  const toastMsg = document.getElementById('feedbackToastMsg');
  let toast; try { toast = new bootstrap.Toast(toastEl, { delay: 3500 }); } catch {}

  // Flag to avoid double-execute loops
  let turnstilePending = false;

  // Called by Turnstile when token is ready (data-callback on the widget)
  window.onTurnstileOK = function () {
    turnstilePending = false;
    // Re-trigger submit; now the hidden token is in the form
    form.requestSubmit();
  };

  form.addEventListener('submit', async (e) => {
    // 1) Ensure the widget has produced a token before we send
    const tokenInput = form.querySelector('input[name="cf-turnstile-response"]');
    const hasToken = !!(tokenInput && tokenInput.value);

    if (!hasToken && window.turnstile) {
      e.preventDefault(); // wait for Turnstile, then our callback will re-submit
      if (!turnstilePending) {
        turnstilePending = true;
        try { turnstile.execute(form); } catch { turnstilePending = false; }
      }
      return;
    }

    // 2) Normal flow (we have token or Turnstile unavailable)
    e.preventDefault();

    if (!form.reportValidity()) return;

    const fd    = new FormData(form); // includes anti-forgery + turnstile token
    const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

    btn.disabled = true;
    spinner.classList.remove('d-none');

    try {
      const resp = await fetch('/Feedback/Send', {
        method: 'POST',
        body: fd,
        // Header is optional if the anti-forgery hidden input is present; keep if your server expects it
        headers: token ? { 'RequestVerificationToken': token } : {}
      });

      const data = await resp.json().catch(() => ({}));

      if (!resp.ok || data.success === false) {
        toastEl.classList.remove('text-bg-success');
        toastEl.classList.add('text-bg-danger');
        toastMsg.textContent = data.message || `Failed to send feedback (HTTP ${resp.status}).`;
        toast?.show();
        return;
      }

      toastEl.classList.remove('text-bg-danger');
      toastEl.classList.add('text-bg-success');
      toastMsg.textContent = 'Thanks! Your feedback was sent.';
      toast?.show();
      form.reset();

      // Optional: reset Turnstile so a new token is generated on the next submit
      try { if (window.turnstile) turnstile.reset(form); } catch {}
    } catch (err) {
      toastEl.classList.remove('text-bg-success');
      toastEl.classList.add('text-bg-danger');
      toastMsg.textContent = 'Network error. Please try again.';
      toast?.show();
    } finally {
      spinner.classList.add('d-none');
      btn.disabled = false;
    }
  });
})();
// ===== End Feedback + Turnstile =====
