// Interactions ported from the Claude Design prototype (copyCmd / setTab).

// Copy the code block associated with the clicked button to the clipboard.
function tsCopy(btn) {
  var block = btn.closest('[data-code]');
  var codeEl = block && block.querySelector('[data-code-text]');
  if (!codeEl) return;
  try {
    if (navigator.clipboard) navigator.clipboard.writeText(codeEl.innerText);
  } catch (err) { /* clipboard unavailable — fail quietly */ }

  var label = btn.querySelector('[data-copy-label]');
  if (label) {
    var prev = label.textContent;
    label.textContent = 'Copied!';
    var prevColor = btn.style.color;
    btn.style.color = '#2DA343';
    setTimeout(function () {
      label.textContent = prev;
      btn.style.color = prevColor;
    }, 1400);
  }
}

// Switch the active quick-start tab and its panel.
function tsSetTab(btn) {
  var idx = btn.getAttribute('data-tab');
  var root = btn.closest('[data-tabs]');
  if (!root) return;

  root.querySelectorAll('[data-tab]').forEach(function (b) {
    var on = b.getAttribute('data-tab') === idx;
    b.style.color = on ? '#00578E' : '#64798d';
    b.style.background = on ? '#fff' : 'transparent';
    b.style.borderBottomColor = on ? '#00578E' : 'transparent';
  });
  root.querySelectorAll('[data-panel]').forEach(function (p) {
    p.style.display = p.getAttribute('data-panel') === idx ? 'block' : 'none';
  });
}
