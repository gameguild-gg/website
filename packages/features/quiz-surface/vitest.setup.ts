class PointerEventStub extends MouseEvent {
    pointerId = 1;
    pointerType = 'mouse';
    isPrimary = true;
    pressure = 0;
    tiltX = 0;
    tiltY = 0;
    width = 1;
    height = 1;
}

if (typeof window !== 'undefined' && !window.PointerEvent) {
    window.PointerEvent = PointerEventStub;
}
