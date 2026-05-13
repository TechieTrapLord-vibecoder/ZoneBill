/**
 * ZoneBill Global Form Validation
 * Provides robust client-side validation with real-time feedback,
 * inline error messages, and visual field states across all forms.
 */
(function () {
    'use strict';

    // ─── Styles injected once ───────────────────────────────────────────────
    var styleId = 'zb-validation-styles';
    if (!document.getElementById(styleId)) {
        var style = document.createElement('style');
        style.id = styleId;
        style.textContent = `
            .zb-field-error input,
            .zb-field-error select,
            .zb-field-error textarea {
                border-color: #ef4444 !important;
                box-shadow: 0 0 0 2px rgba(239,68,68,0.15) !important;
            }
            .zb-field-success input,
            .zb-field-success select,
            .zb-field-success textarea {
                border-color: #22c55e !important;
                box-shadow: 0 0 0 2px rgba(34,197,94,0.12) !important;
            }
            .zb-inline-error {
                display: flex;
                align-items: center;
                gap: 4px;
                color: #fca5a5;
                font-size: 0.78rem;
                margin-top: 4px;
                animation: zbErrIn 0.2s ease both;
            }
            @keyframes zbErrIn {
                from { opacity: 0; transform: translateY(-4px); }
                to   { opacity: 1; transform: translateY(0); }
            }
            .zb-inline-error::before {
                content: '\\f33a';
                font-family: 'bootstrap-icons';
                font-size: 0.85rem;
                flex-shrink: 0;
            }
            .zb-submit-loading {
                opacity: 0.7;
                pointer-events: none;
                cursor: not-allowed;
            }
            .zb-char-count {
                font-size: 0.72rem;
                color: var(--zb-text-muted, #64748b);
                text-align: right;
                margin-top: 2px;
            }
            .zb-char-count.warn { color: #f97316; }
            .zb-char-count.over { color: #ef4444; }
        `;
        document.head.appendChild(style);
    }

    // ─── Helpers ────────────────────────────────────────────────────────────
    function getWrapper(field) {
        return field.closest('.form-group') ||
               field.closest('.col-md-4') ||
               field.closest('.col-md-5') ||
               field.closest('.col-md-3') ||
               field.closest('.col-4') ||
               field.closest('.col-5') ||
               field.closest('.col-8') ||
               field.closest('[data-val-field]') ||
               field.parentElement;
    }

    function setError(field, msg) {
        var wrapper = getWrapper(field);
        wrapper.classList.add('zb-field-error');
        wrapper.classList.remove('zb-field-success');

        var existing = wrapper.querySelector('.zb-inline-error');
        if (existing) { existing.textContent = msg; return; }

        var err = document.createElement('div');
        err.className = 'zb-inline-error';
        err.textContent = msg;
        // Insert after the field or after a .input-group
        var anchor = field.closest('.input-group') || field;
        anchor.insertAdjacentElement('afterend', err);
    }

    function clearError(field) {
        var wrapper = getWrapper(field);
        wrapper.classList.remove('zb-field-error');
        wrapper.classList.add('zb-field-success');
        var err = wrapper.querySelector('.zb-inline-error');
        if (err) err.remove();
    }

    function clearAll(field) {
        var wrapper = getWrapper(field);
        wrapper.classList.remove('zb-field-error', 'zb-field-success');
        var err = wrapper.querySelector('.zb-inline-error');
        if (err) err.remove();
    }

    // ─── Validation Rules ───────────────────────────────────────────────────
    function validateField(field) {
        var val = field.value.trim();
        var type = (field.type || '').toLowerCase();
        var tag  = field.tagName.toLowerCase();

        // Skip hidden, disabled, or submit buttons
        if (type === 'hidden' || type === 'submit' || type === 'button' ||
            field.disabled || field.readOnly) return true;

        // Required
        if (field.hasAttribute('required') && !val) {
            var label = document.querySelector('label[for="' + field.id + '"]');
            var name = label ? label.textContent.trim().replace('*','').trim() : (field.placeholder || 'This field');
            setError(field, name + ' is required.');
            return false;
        }

        // If empty and not required → clear
        if (!val) { clearAll(field); return true; }

        // Email
        if (type === 'email') {
            if (!/^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(val)) {
                setError(field, 'Please enter a valid email address.');
                return false;
            }
        }

        // Min length
        var minLen = parseInt(field.getAttribute('minlength'));
        if (!isNaN(minLen) && val.length < minLen) {
            setError(field, 'Must be at least ' + minLen + ' characters.');
            return false;
        }

        // Max length
        var maxLen = parseInt(field.getAttribute('maxlength'));
        if (!isNaN(maxLen) && val.length > maxLen) {
            setError(field, 'Must not exceed ' + maxLen + ' characters.');
            return false;
        }

        // Min value (for number inputs)
        if (type === 'number') {
            var num = parseFloat(val);
            if (isNaN(num)) {
                setError(field, 'Please enter a valid number.');
                return false;
            }
            var minVal = field.getAttribute('min');
            var maxVal = field.getAttribute('max');
            if (minVal !== null && num < parseFloat(minVal)) {
                setError(field, 'Value must be at least ' + minVal + '.');
                return false;
            }
            if (maxVal !== null && num > parseFloat(maxVal)) {
                setError(field, 'Value must not exceed ' + maxVal + '.');
                return false;
            }
        }

        // Custom pattern
        var pattern = field.getAttribute('pattern');
        if (pattern) {
            try {
                if (!(new RegExp('^(?:' + pattern + ')$')).test(val)) {
                    var title = field.getAttribute('title') || 'Please match the required format.';
                    setError(field, title);
                    return false;
                }
            } catch (e) { /* ignore bad regex */ }
        }

        // Select: must not be empty/placeholder
        if (tag === 'select' && !val) {
            setError(field, 'Please select an option.');
            return false;
        }

        clearError(field);
        return true;
    }

    // ─── Password Confirm Matching ───────────────────────────────────────────
    function checkPasswordMatch(confirmField) {
        var form = confirmField.closest('form');
        if (!form) return true;
        var pwdField = form.querySelector('input[type="password"]:not([name="confirmPassword"]):not([id="confirmPassword"])');
        if (!pwdField) return true;
        if (confirmField.value && confirmField.value !== pwdField.value) {
            setError(confirmField, 'Passwords do not match.');
            return false;
        }
        clearError(confirmField);
        return true;
    }

    // ─── Character counter for textareas and long inputs ────────────────────
    function attachCharCounter(field) {
        var maxLen = parseInt(field.getAttribute('maxlength'));
        if (isNaN(maxLen) || maxLen < 20) return; // only worthwhile for longer fields

        var counter = document.createElement('div');
        counter.className = 'zb-char-count';
        field.insertAdjacentElement('afterend', counter);

        function update() {
            var remaining = maxLen - field.value.length;
            counter.textContent = remaining + ' characters remaining';
            counter.className = 'zb-char-count' +
                (remaining < 20 ? ' over' : remaining < 40 ? ' warn' : '');
        }
        field.addEventListener('input', update);
        update();
    }

    // ─── Form Submit Guard ───────────────────────────────────────────────────
    function attachForm(form) {
        if (form.dataset.zbValidated) return;
        form.dataset.zbValidated = '1';

        var fields = form.querySelectorAll('input, select, textarea');

        // Attach blur-time validation (real-time feedback after leaving field)
        fields.forEach(function (f) {
            if (f.type === 'hidden' || f.type === 'submit') return;

            // Char counter
            if (f.tagName.toLowerCase() === 'textarea' || parseInt(f.getAttribute('maxlength')) >= 50) {
                attachCharCounter(f);
            }

            f.addEventListener('blur', function () {
                if (f.name === 'confirmPassword' || f.id === 'confirmPassword') {
                    checkPasswordMatch(f);
                } else {
                    validateField(f);
                }
            });

            // Clear error on input
            f.addEventListener('input', function () {
                var wrapper = getWrapper(f);
                if (wrapper.classList.contains('zb-field-error')) {
                    if (f.name === 'confirmPassword' || f.id === 'confirmPassword') {
                        checkPasswordMatch(f);
                    } else {
                        validateField(f);
                    }
                }
            });
        });

        // Submit guard
        form.addEventListener('submit', function (e) {
            var valid = true;
            var firstInvalid = null;

            fields.forEach(function (f) {
                if (f.type === 'hidden' || f.type === 'submit') return;
                var ok;
                if (f.name === 'confirmPassword' || f.id === 'confirmPassword') {
                    ok = checkPasswordMatch(f);
                } else {
                    ok = validateField(f);
                }
                if (!ok) {
                    valid = false;
                    if (!firstInvalid) firstInvalid = f;
                }
            });

            if (!valid) {
                e.preventDefault();
                e.stopImmediatePropagation();
                if (firstInvalid) {
                    firstInvalid.focus();
                    firstInvalid.scrollIntoView({ behavior: 'smooth', block: 'center' });
                }
                return;
            }

            // Prevent double-submit
            var submitBtn = form.querySelector('[type="submit"]');
            if (submitBtn) {
                submitBtn.classList.add('zb-submit-loading');
                var originalText = submitBtn.innerHTML;
                submitBtn.innerHTML = '<i class="bi bi-arrow-repeat me-1" style="animation:spin 1s linear infinite;display:inline-block;"></i> Processing...';

                // Auto-reset if server returns within 10s (e.g. validation error)
                setTimeout(function () {
                    submitBtn.classList.remove('zb-submit-loading');
                    submitBtn.innerHTML = originalText;
                }, 10000);

                // Add spin keyframe if not present
                if (!document.getElementById('zb-spin-kf')) {
                    var kf = document.createElement('style');
                    kf.id = 'zb-spin-kf';
                    kf.textContent = '@keyframes spin { to { transform: rotate(360deg); } }';
                    document.head.appendChild(kf);
                }
            }
        });
    }

    // ─── Initialize & watch for dynamic forms ───────────────────────────────
    function init() {
        document.querySelectorAll('form').forEach(attachForm);
    }

    // MutationObserver for dynamically added forms (modals, SPA-like panels)
    var observer = new MutationObserver(function (mutations) {
        mutations.forEach(function (m) {
            m.addedNodes.forEach(function (node) {
                if (node.nodeType !== 1) return;
                if (node.tagName === 'FORM') {
                    attachForm(node);
                } else {
                    node.querySelectorAll && node.querySelectorAll('form').forEach(attachForm);
                }
            });
        });
    });

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () {
            init();
            observer.observe(document.body, { childList: true, subtree: true });
        });
    } else {
        init();
        observer.observe(document.body, { childList: true, subtree: true });
    }

})();
