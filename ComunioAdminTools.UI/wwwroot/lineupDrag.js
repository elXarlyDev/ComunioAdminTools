const bindings = new WeakMap();

export function detach(root) {
    bindings.get(root)?.();
    bindings.delete(root);
}

export function attach(root, callback) {
    detach(root);
    if (!root?.isConnected) return;

    let gesture = null;
    let timer = null;
    let frame = null;
    let ghost = null;
    let destination = null;
    let pending = false;

    function reset() {
        clearTimeout(timer);
        cancelAnimationFrame(frame);
        gesture?.source.classList.remove('drag-selected');
        destination?.classList.remove('lineup-drop-target');
        ghost?.remove();
        gesture = null;
        destination = null;
        ghost = null;
        timer = null;
        frame = null;
    }

    function available() {
        return !pending && root.isConnected && !root.closest('[inert]');
    }

    function target(element) {
        const result = element?.closest('[data-lineup-slot], [data-lineup-substitute], [data-lineup-bench]');
        if (!result || !root.contains(result)) return null;

        const source = gesture?.source;
        if (result.hasAttribute('data-lineup-slot')) {
            if (source?.dataset.playerPosition !== result.dataset.lineupPosition) return null;
            if (source.hasAttribute('data-lineup-slot') && result.dataset.lineupPlayer
                && result.dataset.playerPosition !== source.dataset.lineupPosition) return null;
        } else if (result.hasAttribute('data-lineup-substitute') && source?.hasAttribute('data-lineup-slot')
            && result.dataset.playerPosition !== source.dataset.lineupPosition) return null;

        return result;
    }

    function highlight(element) {
        destination?.classList.remove('lineup-drop-target');
        destination = target(element);
        destination?.classList.add('lineup-drop-target');
    }

    function update(x, y) {
        gesture.x = x;
        gesture.y = y;
        ghost.style.transform = `translate(${x - gesture.offsetX}px, ${y - gesture.offsetY}px)`;
        highlight(document.elementFromPoint(x, y));
    }

    function scroll() {
        if (!gesture?.dragging) return;
        if (!available() || !root.contains(gesture.source)) {
            reset();
            return;
        }

        const direction = gesture.y < 60 ? -1 : gesture.y > innerHeight - 60 ? 1 : 0;
        if (direction) {
            window.scrollBy(0, direction * 10);
            highlight(document.elementFromPoint(gesture.x, gesture.y));
        }

        frame = requestAnimationFrame(scroll);
    }

    function begin() {
        if (!gesture || !available()) {
            reset();
            return;
        }

        const bounds = gesture.source.getBoundingClientRect();
        gesture.dragging = true;
        gesture.offsetX = Math.min(gesture.x - bounds.left, bounds.width);
        gesture.offsetY = Math.min(gesture.y - bounds.top, bounds.height);
        ghost = gesture.source.cloneNode(true);
        ghost.setAttribute('aria-hidden', 'true');
        ghost.removeAttribute('id');
        for (const element of ghost.querySelectorAll('[id]')) element.removeAttribute('id');
        ghost.removeAttribute('tabindex');
        Object.assign(ghost.style, {
            position: 'fixed', left: '0', top: '0', margin: '0', zIndex: '10000',
            pointerEvents: 'none', width: `${bounds.width}px`, opacity: '.9',
            font: getComputedStyle(gesture.source).font, boxShadow: '0 4px 16px #0004'
        });
        document.body.append(ghost);
        gesture.source.classList.add('drag-selected');
        update(gesture.x, gesture.y);
        frame = requestAnimationFrame(scroll);
    }

    function finish(element) {
        const playerId = gesture?.source.dataset.lineupPlayer;
        const drop = target(element);
        const valid = available() && gesture && root.contains(gesture.source) && drop;
        const slot = drop?.hasAttribute('data-lineup-slot') ? Number(drop.dataset.lineupSlot) : null;
        const substituteId = drop?.dataset.lineupSubstitute ?? null;
        reset();
        if (!valid) return;

        pending = true;
        callback.invokeMethodAsync('MoveLineupPlayer', playerId, slot, substituteId)
            .catch(error => console.error('Lineup move failed', error))
            .finally(() => { pending = false; });
    }

    function pointerDown(event) {
        if (event.pointerType === 'touch' || event.button !== 0 || !event.isPrimary || !available()) return;
        const source = event.target.closest('[data-lineup-player]');
        if (!source || !root.contains(source)) return;

        reset();
        gesture = { source, x: event.clientX, y: event.clientY, pointerId: event.pointerId };
    }

    function pointerMove(event) {
        if (!gesture || gesture.pointerId !== event.pointerId) return;
        if (!gesture.dragging && Math.hypot(event.clientX - gesture.x, event.clientY - gesture.y) > 6) begin();
        if (!gesture?.dragging) return;

        event.preventDefault();
        update(event.clientX, event.clientY);
    }

    function pointerUp(event) {
        if (!gesture || gesture.pointerId !== event.pointerId) return;
        if (gesture.dragging) finish(document.elementFromPoint(event.clientX, event.clientY));
        else reset();
    }

    function touchStart(event) {
        reset();
        if (event.touches.length !== 1 || !available()) return;
        const source = event.target.closest('[data-lineup-player]');
        if (!source || !root.contains(source)) return;

        const touch = event.touches[0];
        gesture = { source, x: touch.clientX, y: touch.clientY, touchId: touch.identifier };
        timer = setTimeout(begin, 220);
    }

    function touchMove(event) {
        if (!gesture || gesture.touchId === undefined) return;
        const touch = [...event.touches].find(touch => touch.identifier === gesture.touchId);
        if (!touch || event.touches.length !== 1) {
            reset();
            return;
        }

        if (!gesture.dragging) {
            if (Math.hypot(touch.clientX - gesture.x, touch.clientY - gesture.y) > 8) reset();
            return;
        }

        event.preventDefault();
        update(touch.clientX, touch.clientY);
    }

    function touchEnd(event) {
        if (!gesture || gesture.touchId === undefined) return;
        const touch = [...event.changedTouches].find(touch => touch.identifier === gesture.touchId);
        if (!touch) return;
        if (gesture.dragging) {
            event.preventDefault();
            finish(document.elementFromPoint(touch.clientX, touch.clientY));
        } else reset();
    }

    function keyDown(event) {
        if (event.key === 'Escape') { reset(); return; }
        if (!['Enter', ' '].includes(event.key) || !available()) return;
        event.preventDefault();

        if (gesture) {
            finish(event.target);
        } else {
            const source = event.target.closest('[data-lineup-player]');
            if (source && root.contains(source)) {
                gesture = { source };
                source.classList.add('drag-selected');
            }
        }
    }

    const events = [
        [root, 'pointerdown', pointerDown], [window, 'pointermove', pointerMove],
        [window, 'pointerup', pointerUp], [window, 'pointercancel', reset],
        [root, 'touchstart', touchStart, { passive: true }],
        [window, 'touchmove', touchMove, { passive: false }],
        [window, 'touchend', touchEnd, { passive: false }], [window, 'touchcancel', reset],
        [root, 'keydown', keyDown], [window, 'blur', reset]
    ];
    for (const [element, name, listener, options] of events) element.addEventListener(name, listener, options);
    bindings.set(root, () => {
        reset();
        for (const [element, name, listener, options] of events) element.removeEventListener(name, listener, options);
    });
}
