figma.showUI(__html__, { width: 320, height: 205, themeColors: true });

const MIN_EXPORT_LONG_SIDE = 256;
const LARGE_IMAGE_EXPORT_SCALE = 2;
const LARGE_IMAGE_MIN_AREA_RATIO = 0.20;
const LARGE_IMAGE_MIN_AXIS_RATIO = 0.70;

figma.ui.onmessage = async (message) => {
  if (message.type !== 'import') return;
  let overallProgress = 0;
  const postStatus = (text, busy, progress, state) => {
    if (typeof progress === 'number')
      overallProgress = Math.max(overallProgress, Math.min(1, Math.max(0, progress)));
    figma.ui.postMessage({ message: text, busy, progress: overallProgress, state });
  };
  try {
    const selection = figma.currentPage.selection;
    if (selection.length === 0 || selection.some(node => node.type !== 'FRAME'))
      throw new Error('Select one or more Figma Frames only.');

    const frameCount = selection.length;
    const frameWeights = selection.map(countSerializableNodes);
    const totalWeight = Math.max(1, frameWeights.reduce((sum, weight) => sum + weight, 0));
    let completedWeight = 0;
    for (let index = 0; index < frameCount; index++) {
      const frame = selection[index];
      const frameWeight = frameWeights[index];
      const reportFrameProgress = (localProgress, text) => {
        const overall = (completedWeight + frameWeight * Math.max(0, Math.min(1, localProgress))) / totalWeight;
        postStatus(text, true, overall, 'active');
      };
      reportFrameProgress(0, `Reading and exporting ${index + 1}/${frameCount}: “${frame.name}”…`);
      const serializationProgress = {
        completed: 0,
        total: frameWeight,
        lastPostedAt: 0,
        report: fraction => reportFrameProgress(
          fraction * 0.5,
          `Reading and exporting ${index + 1}/${frameCount}: “${frame.name}”…`)
      };
      let frameData = await serializeNode(
        frame, null, true, 1, 1, [],
        Math.max(0.01, finite(frame.width)),
        Math.max(0.01, finite(frame.height)),
        serializationProgress);

      reportFrameProgress(0.55, `Sending ${index + 1}/${frameCount}: “${frame.name}”…`);
      let payload = {
        protocolVersion: 9,
        documentName: figma.root.name,
        frame: frameData
      };
      frameData = null;

      // Start the request before dropping our final payload reference. sendToUnity
      // stringifies synchronously, so only one Frame is ever retained as JSON/base64.
      const responsePromise = sendToUnity(payload, (progress, text) =>
        reportFrameProgress(0.55 + progress * 0.45, text));
      payload = null;
      const response = await responsePromise;
      const result = await response.json();
      if (!response.ok || !result.ok)
        throw new Error(`Frame ${index + 1}/${frameCount} “${frame.name}”: ${result.message || `Unity returned HTTP ${response.status}.`}`);

      await waitForUnityReady((progress, unityMessage) => reportFrameProgress(
        0.55 + progress * 0.45,
        `Importing ${index + 1}/${frameCount} in Unity — ${unityMessage}`));
      reportFrameProgress(1, `Imported ${index + 1}/${frameCount}: “${frame.name}”`);
      completedWeight += frameWeight;
    }

    figma.notify(`Queued ${frameCount} frame${frameCount === 1 ? '' : 's'} in Unity`);
    postStatus(`Done. Unity imported ${frameCount} selected frame${frameCount === 1 ? '' : 's'}.`, false, 1, 'done');
  } catch (error) {
    const text = error && error.message ? error.message : String(error);
    figma.notify(text, { error: true });
    postStatus(`Import failed: ${text}\n\nOpen Unity and check Tools → Figma Frame Importer.`, false, overallProgress, 'error');
  }
};

async function sendToUnity(payload, reportUnityProgress) {
  const body = JSON.stringify(payload);
  payload = null;
  await waitForUnityReady(reportUnityProgress);
  let lastError = null;
  let networkAttempt = 0;
  let busyRaceCount = 0;
  while (networkAttempt < 10 && busyRaceCount < 20) {
    try {
      const response = await fetch('http://localhost:38457/import', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body
      });
      if (response.status !== 429) return response;
      busyRaceCount++;
      await waitForUnityReady();
    } catch (error) {
      lastError = error;
      networkAttempt++;
      if (networkAttempt < 10) {
        if (reportUnityProgress)
          reportUnityProgress(0, `Waiting for Unity bridge… ${networkAttempt}/10`);
        await delay(750);
      }
    }
  }
  if (busyRaceCount >= 20)
    throw new Error('Unity bridge stayed busy after repeated readiness checks.');
  const details = lastError ? String(lastError.message || lastError) : '';
  if (/out of memory/i.test(details))
    throw new Error(`The current Frame is too large to serialize or send: ${details}`);
  throw new Error(`Unity bridge is not listening on localhost:38457. Focus Unity and wait for script compilation.${details ? ` ${details}` : ''}`);
}

async function waitForUnityReady(reportUnityProgress) {
  for (let attempt = 0; attempt < 2400; attempt++) {
    try {
      const response = await fetch('http://localhost:38457/status');
      // Compatibility with an older running bridge: POST will provide the
      // normal connection error or queue response.
      if (response.status === 404) return;
      if (response.ok) {
        const status = await response.json();
        if (!status.busy) return;
        if (reportUnityProgress)
          reportUnityProgress(Number(status.progress) || 0, status.message || 'Working…');
      }
    } catch (_) {
      return;
    }
    if (reportUnityProgress && attempt % 4 === 0)
      reportUnityProgress(0, `Waiting for Unity… ${Math.ceil((attempt + 1) * 0.75)}s`);
    await delay(750);
  }
  throw new Error('Unity did not finish the previous Frame within 30 minutes.');
}

function delay(milliseconds) {
  return new Promise(resolve => setTimeout(resolve, milliseconds));
}

async function serializeNode(node, parent, isRoot, inheritedScaleX, inheritedScaleY, inheritedEffects,
  referenceFrameWidth, referenceFrameHeight, progressContext) {
  const hasChildren = 'children' in node && node.children.length > 0;
  const isText = node.type === 'TEXT';
  const interactive = detectInteractive(node);
  const geometry = isRoot ? identityGeometry() : getLocalGeometry(node, parent);
  const parentScaleX = finite(inheritedScaleX, 1) || 1;
  const parentScaleY = finite(inheritedScaleY, 1) || 1;
  // Dimensions are flattened using scale magnitudes, while reflection signs
  // stay on the exact Figma node and are reproduced by its Unity RectTransform.
  // Propagating a negative sign into descendants would apply the same flip twice.
  const localScaleX = finite(geometry.scaleX, 1) || 1;
  const localScaleY = finite(geometry.scaleY, 1) || 1;
  const effectiveScaleX = parentScaleX * Math.abs(localScaleX);
  const effectiveScaleY = parentScaleY * Math.abs(localScaleY);
  const ownEffects = getVisibleEffects(node);
  const delegatedEffects = Array.isArray(inheritedEffects) ? inheritedEffects : [];
  const effectiveEffects = ownEffects.concat(delegatedEffects);
  const solid = !isText && effectiveEffects.length === 0 ? getSimpleSolidFill(node) : null;
  const tintableGradient = !isText ? getTintableMonochromeGradient(node, effectiveEffects) : null;
  const assetReuseKey = !isText ? getAssetReuseKey(node, effectiveEffects) : '';
  const constraints = 'constraints' in node && node.constraints ? node.constraints : null;

  const data = {
    id: node.id,
    name: node.name || node.type,
    type: node.type,
    renderMode: isText ? 'text' : hasChildren ? 'container' : solid ? 'solid' : 'image',
    x: geometry.x * parentScaleX,
    y: geometry.y * parentScaleY,
    width: finite(node.width) * effectiveScaleX,
    height: finite(node.height) * effectiveScaleY,
    rotation: geometry.rotation,
    scaleX: localScaleX < 0 ? -1 : 1,
    scaleY: localScaleY < 0 ? -1 : 1,
    constraintHorizontal: constraints ? constraints.horizontal : 'MIN',
    constraintVertical: constraints ? constraints.vertical : 'MIN',
    overflowDirection: getStringProperty(node, 'overflowDirection', 'NONE'),
    layoutMode: getStringProperty(node, 'layoutMode', 'NONE'),
    opacity: 'opacity' in node ? finite(node.opacity, 1) : 1,
    visible: node.visible !== false,
    interactive,
    clipsContent: 'clipsContent' in node && node.clipsContent === true,
    isMask: 'isMask' in node && node.isMask === true,
    hasEffects: effectiveEffects.length > 0,
    cornerRadius: getCornerRadius(node) * Math.min(effectiveScaleX, effectiveScaleY),
    color: solid,
    text: '',
    fontFamily: '',
    fontStyle: '',
    fontSize: 0,
    textAlignHorizontal: 'LEFT',
    textAlignVertical: 'TOP',
    textAutoResize: '',
    lineHeight: 0,
    letterSpacing: 0,
    textShadows: [],
    animationDuration: 0,
    animationTimelineId: '',
    animationTracks: [],
    imageMime: '',
    imageBase64: '',
    imageScaleMode: !isText ? getImageScaleMode(node) : '',
    imageTint: tintableGradient ? tintableGradient.tint : [1, 1, 1, 1],
    imageIsTintMask: tintableGradient !== null,
    assetReuseKey,
    visualX: 0,
    visualY: 0,
    visualWidth: finite(node.width) * effectiveScaleX,
    visualHeight: finite(node.height) * effectiveScaleY,
    children: []
  };
  fillAnimationData(data, node);
  const imageExportScale = getImageExportScale(
    node, data.width, data.height, referenceFrameWidth, referenceFrameHeight);

  if (isText) {
    fillTextData(data, node, effectiveScaleX, effectiveScaleY);
  } else if (hasChildren) {
    // Strict layer mode: only the node's own fill/stroke is exported here.
    // Descendants are never flattened into a container bitmap.
    if (!solid && hasDirectPaintOrStroke(node))
      applyExport(data, tintableGradient
        ? await exportCanonicalGradientMask(tintableGradient, data.width, data.height)
        : await exportOwnVisual(node, delegatedEffects, imageExportScale));
  } else if (!solid && 'exportAsync' in node) {
    let exported = tintableGradient
      ? await exportCanonicalGradientMask(tintableGradient, data.width, data.height)
      // A reflected rectangle clone keeps an implicit Figma reflection even
      // after clone.rotation is reset. Its PNG can therefore be exported
      // upside-down and then transformed a second time by Unity. Rebuilding the
      // rectangle from its paints exports transform-neutral pixels; the signed
      // RectTransform is the single source of rotation/reflection in Unity.
      : node.type === 'RECTANGLE'
        ? await exportOwnVisual(node, delegatedEffects, imageExportScale)
        : await exportVisual(node, data.width, data.height, data.isMask, delegatedEffects, imageExportScale);
    // Figma normally exports an isolated mask as the result of the mask operation.
    // Without the masked siblings that result can be a transparent 1x1 PNG. A
    // rectangle copy is a reliable fallback for the mask graphic itself.
    if (data.isMask && isDegenerateMaskExport(exported, data) && node.type === 'RECTANGLE')
      exported = await exportOwnVisual(node, delegatedEffects, imageExportScale);
    applyExport(data, exported);
  }

  if (hasChildren) {
    // Effects on a Figma Group belong to the parent compositing layer. When the
    // group has a full-bounds background child (the standard button/card shape),
    // that child is the exact alpha host for inner/drop shadows. Delegate the
    // parent effects to that one visual only; applying them to every child would
    // incorrectly create shadows around labels and icons.
    const effectHostId = !isText && effectiveEffects.length > 0 && !hasDirectPaintOrStroke(node)
      ? findFullBoundsEffectHost(node)
      : null;
    for (const child of node.children) {
      if (child.type === 'SLICE') continue;
      const childEffects = effectHostId === child.id ? effectiveEffects : [];
      data.children.push(await serializeNode(
        child, node, false, effectiveScaleX, effectiveScaleY, childEffects,
        referenceFrameWidth, referenceFrameHeight, progressContext));
    }
  }
  if (progressContext) {
    progressContext.completed++;
    const now = Date.now();
    if (progressContext.completed >= progressContext.total || now - progressContext.lastPostedAt >= 100) {
      progressContext.lastPostedAt = now;
      progressContext.report(progressContext.completed / Math.max(1, progressContext.total));
    }
  }
  return data;
}

function countSerializableNodes(node) {
  if (!node || node.type === 'SLICE') return 0;
  if (!('children' in node)) return 1;
  return 1 + node.children.reduce((sum, child) => sum + countSerializableNodes(child), 0);
}

function fillAnimationData(data, node) {
  // Figma's timeline surface is intentionally read-only here. Manual tracks
  // contain the actual authored keyframes without flattening the whole frame
  // into one animation. The Unity side groups them by their real layer owner.
  let tracks = null;
  let timelines = null;
  try { tracks = node.manualKeyframeTracks; } catch (_) { tracks = null; }
  try { timelines = node.timelines; } catch (_) { timelines = null; }
  if (!tracks || typeof tracks !== 'object') return;

  if (Array.isArray(timelines) && timelines.length > 0) {
    const timeline = timelines[0];
    data.animationTimelineId = timeline && timeline.id ? String(timeline.id) : '';
    data.animationDuration = Math.max(0, finite(timeline && timeline.duration));
  }

  for (const property of Object.keys(tracks)) {
    const track = tracks[property];
    if (!track || !Array.isArray(track.keyframes) || track.keyframes.length === 0) continue;
    const keyframes = [];
    for (const source of track.keyframes) {
      if (!source || !source.value || source.value.type !== 'FLOAT') continue;
      const easing = source.easing || {};
      const bezier = easing.easingFunctionCubicBezier || {};
      keyframes.push({
        time: Math.max(0, finite(source.timelinePosition)),
        value: finite(source.value.value),
        easing: typeof easing.type === 'string' ? easing.type : 'LINEAR',
        x1: finite(bezier.x1),
        y1: finite(bezier.y1),
        x2: finite(bezier.x2, 1),
        y2: finite(bezier.y2, 1)
      });
    }
    if (keyframes.length === 0) continue;
    keyframes.sort((a, b) => a.time - b.time);
    data.animationTracks.push({
      property,
      baseValue: track.baseValue && track.baseValue.type === 'FLOAT'
        ? finite(track.baseValue.value)
        : 0,
      keyframes
    });
  }
}

async function exportCanonicalGradientMask(gradient, targetWidth, targetHeight) {
  const rect = figma.createRectangle();
  try {
    // A fixed-size white/greyscale source makes identical gradient masks byte-
    // identical even when their Figma rectangles have different dimensions.
    // Unity stretches this canonical sprite and restores color with Image.color.
    rect.resize(64, 64);
    rect.fills = [gradient.paint];
    rect.strokes = [];
    rect.opacity = 1;
    const bytes = await rect.exportAsync({
      format: 'PNG',
      constraint: getAdaptiveExportConstraint(rect.width, rect.height)
    });
    const width = Math.max(0.01, finite(targetWidth));
    const height = Math.max(0.01, finite(targetHeight));
    const layout = { x: 0, y: 0, width, height };
    return { bytes, layout, bounds: { x: 0, y: 0, width, height } };
  } finally {
    rect.remove();
  }
}

function identityGeometry() {
  return { x: 0, y: 0, rotation: 0, scaleX: 1, scaleY: 1 };
}

function getStringProperty(node, property, fallback) {
  if (!(property in node)) return fallback;
  const value = node[property];
  return typeof value === 'string' ? value : fallback;
}

function applyExport(data, exported) {
  if (!exported || !exported.bytes || !exported.layout || !exported.bounds) return;
  data.imageMime = 'image/png';
  data.imageBase64 = bytesToBase64(exported.bytes);
  const toDataX = data.width / exported.layout.width;
  const toDataY = data.height / exported.layout.height;
  data.visualX = (exported.bounds.x - exported.layout.x) * toDataX;
  data.visualY = (exported.bounds.y - exported.layout.y) * toDataY;
  data.visualWidth = exported.bounds.width * toDataX;
  data.visualHeight = exported.bounds.height * toDataY;
}

function detectInteractive(node) {
  if ('reactions' in node && Array.isArray(node.reactions) && node.reactions.length > 0) return true;
  if (node.type === 'TEXT' || !('children' in node) || node.children.length === 0) return false;
  const name = (node.name || '').toLowerCase();
  const namedAction = /(^|[\s_\-/])(button|btn|cta|tab|toggle|checkbox|radio|кнопка|price|buy|purchase|play|open)([\s_\-/]|$)/.test(name);
  if (finite(node.height) > 160 && !namedAction) return false;
  const actionPattern = /(free|open|play|buy|shop|main story|side stories|\$|€|£|₽|\d+[,.]\d{2})/i;
  if (!namedAction && !actionPattern.test(collectText(node, 0, 4))) return false;
  const hasVisualChild = node.children.some(child => child.visible !== false && child.type !== 'TEXT' &&
    (hasDirectPaintOrStroke(child) || ('children' in child && child.children.length > 0)));
  if (!hasVisualChild) return false;
  // Only the smallest wrapper around an action becomes a Button. Labels and
  // backgrounds stay separate children and never receive duplicate Buttons.
  return !node.children.some(child =>
    child.visible !== false && child.type !== 'TEXT' && detectInteractive(child));
}

function collectText(node, depth, maxDepth) {
  if (depth > maxDepth) return '';
  if (node.type === 'TEXT') return node.characters || '';
  if (!('children' in node)) return '';
  return node.children.map(child => collectText(child, depth + 1, maxDepth)).join(' ');
}

async function exportVisual(node, targetWidth, targetHeight, clearMask, extraEffects, exportScale) {
  const clone = node.clone();
  try {
    clone.visible = true;
    clone.opacity = 1;
    // Export the mask's own alpha geometry, not an already-applied mask result.
    // The clone is detached from the original sibling stack, so leaving isMask
    // enabled makes Figma return an empty/1x1 image for many valid masks.
    if (clearMask) {
      try { clone.isMask = false; } catch (_) {}
    }
    appendEffects(clone, extraEffects);
    try { clone.rotation = 0; } catch (_) {}
    const cloneLayout = clone.absoluteBoundingBox;
    if (!cloneLayout || cloneLayout.width <= 0 || cloneLayout.height <= 0) return null;
    const bytes = await clone.exportAsync({
      format: 'PNG',
      constraint: getAdaptiveExportConstraint(cloneLayout.width, cloneLayout.height, exportScale)
    });
    const pngSize = getPngPixelSize(bytes);
    if (!pngSize) return null;

    // clone() may be placed at a completely different absolute page position.
    // Export geometry must therefore be converted to node-local coordinates
    // before it is sent to Unity; absolute Figma page X/Y must never leak here.
    const width = Math.max(0.01, finite(targetWidth, cloneLayout.width));
    const height = Math.max(0.01, finite(targetHeight, cloneLayout.height));
    const toLocalX = width / cloneLayout.width;
    const toLocalY = height / cloneLayout.height;
    const cloneRender = clone.absoluteRenderBounds;
    const layout = { x: 0, y: 0, width, height };
    let bounds;
    if (cloneRender) {
      bounds = {
        x: (cloneRender.x - cloneLayout.x) * toLocalX,
        y: (cloneRender.y - cloneLayout.y) * toLocalY,
        width: cloneRender.width * toLocalX,
        height: cloneRender.height * toLocalY
      };
    } else {
      bounds = { x: layout.x, y: layout.y, width: layout.width, height: layout.height };
    }
    return { bytes, layout, bounds };
  } finally {
    clone.remove();
  }
}

function isDegenerateMaskExport(exported, data) {
  if (!exported || !exported.bounds) return true;
  if (finite(data.width) <= 1.5 || finite(data.height) <= 1.5) return false;
  return exported.bounds.width <= 1.5 || exported.bounds.height <= 1.5;
}

async function exportOwnVisual(node, extraEffects, exportScale) {
  const rect = figma.createRectangle();
  try {
    rect.resize(Math.max(0.01, finite(node.width)), Math.max(0.01, finite(node.height)));
    copyProperty(node, rect, 'fills');
    copyProperty(node, rect, 'strokes');
    copyProperty(node, rect, 'strokeWeight');
    copyProperty(node, rect, 'strokeAlign');
    copyProperty(node, rect, 'strokeTopWeight');
    copyProperty(node, rect, 'strokeRightWeight');
    copyProperty(node, rect, 'strokeBottomWeight');
    copyProperty(node, rect, 'strokeLeftWeight');
    copyProperty(node, rect, 'cornerRadius');
    copyProperty(node, rect, 'topLeftRadius');
    copyProperty(node, rect, 'topRightRadius');
    copyProperty(node, rect, 'bottomRightRadius');
    copyProperty(node, rect, 'bottomLeftRadius');
    // Keep every enabled Figma effect. Inner shadows stay inside the source
    // geometry, while drop shadows and layer blur expand absoluteRenderBounds.
    // Background blur is retained too; Figma decides what can be represented in
    // the isolated export rather than the importer silently deleting the effect.
    if ('effects' in node && node.effects !== figma.mixed)
      rect.effects = node.effects.filter(effect => effect.visible !== false);
    appendEffects(rect, extraEffects);
    rect.opacity = 1;
    const bytes = await rect.exportAsync({
      format: 'PNG',
      constraint: getAdaptiveExportConstraint(rect.width, rect.height, exportScale)
    });
    const layout = rect.absoluteBoundingBox;
    const pngSize = getPngPixelSize(bytes);
    const render = rect.absoluteRenderBounds;
    if (!layout || !pngSize) return null;
    // Do not center the exported pixels around the source bounds: an offset
    // shadow is intentionally asymmetric. absoluteRenderBounds is the exact
    // Figma-space rectangle occupied by fill, stroke, blur and shadows.
    const bounds = render && render.width > 0 && render.height > 0
      ? { x: render.x, y: render.y, width: render.width, height: render.height }
      : { x: layout.x, y: layout.y, width: layout.width, height: layout.height };
    return { bytes, layout, bounds };
  } finally {
    rect.remove();
  }
}

function getAdaptiveExportConstraint(width, height, requestedScale) {
  const safeWidth = Math.max(0.01, finite(width));
  const safeHeight = Math.max(0.01, finite(height));
  const longSide = Math.max(safeWidth, safeHeight);
  const scale = Math.max(1, finite(requestedScale, 1));
  if (scale > 1 && longSide * scale >= MIN_EXPORT_LONG_SIDE)
    return { type: 'SCALE', value: scale };
  if (longSide >= MIN_EXPORT_LONG_SIDE) return { type: 'SCALE', value: 1 };
  return safeWidth >= safeHeight
    ? { type: 'WIDTH', value: MIN_EXPORT_LONG_SIDE }
    : { type: 'HEIGHT', value: MIN_EXPORT_LONG_SIDE };
}

function getImageExportScale(node, width, height, frameWidth, frameHeight) {
  if (!hasVisibleImageFill(node)) return 1;
  const safeFrameWidth = Math.max(0.01, finite(frameWidth));
  const safeFrameHeight = Math.max(0.01, finite(frameHeight));
  const widthRatio = Math.max(0, finite(width)) / safeFrameWidth;
  const heightRatio = Math.max(0, finite(height)) / safeFrameHeight;
  const areaRatio = widthRatio * heightRatio;
  return areaRatio >= LARGE_IMAGE_MIN_AREA_RATIO ||
    widthRatio >= LARGE_IMAGE_MIN_AXIS_RATIO ||
    heightRatio >= LARGE_IMAGE_MIN_AXIS_RATIO
    ? LARGE_IMAGE_EXPORT_SCALE
    : 1;
}

function hasVisibleImageFill(node) {
  return 'fills' in node && node.fills !== figma.mixed && Array.isArray(node.fills) &&
    node.fills.some(paint => paint && paint.visible !== false && paint.type === 'IMAGE');
}

function getPngPixelSize(bytes) {
  if (!bytes || bytes.length < 24) return null;
  const width = readUint32(bytes, 16);
  const height = readUint32(bytes, 20);
  return width > 0 && height > 0 ? { width, height } : null;
}

function readUint32(bytes, offset) {
  return ((bytes[offset] * 0x1000000) + (bytes[offset + 1] << 16) +
    (bytes[offset + 2] << 8) + bytes[offset + 3]) >>> 0;
}

function getLocalGeometry(node, parent) {
  if (!parent || !node.absoluteTransform || !parent.absoluteTransform)
    return { x: finite(node.x), y: finite(node.y), rotation: finite(node.rotation), scaleX: 1, scaleY: 1 };
  const child = node.absoluteTransform, basis = parent.absoluteTransform;
  const a = basis[0][0], c = basis[0][1], tx = basis[0][2];
  const b = basis[1][0], d = basis[1][1], ty = basis[1][2];
  const det = a * d - b * c;
  if (Math.abs(det) < 0.000001)
    return { x: finite(node.x), y: finite(node.y), rotation: finite(node.rotation), scaleX: 1, scaleY: 1 };
  const dx = child[0][2] - tx, dy = child[1][2] - ty;
  const r00 = (d * child[0][0] - c * child[1][0]) / det;
  const r10 = (-b * child[0][0] + a * child[1][0]) / det;
  const r01 = (d * child[0][1] - c * child[1][1]) / det;
  const r11 = (-b * child[0][1] + a * child[1][1]) / det;
  // Decompose M = R * S with a positive Y scale. A reflected determinant is
  // represented as scaleX=-1, matching Figma's Flip Horizontal and Unity's UI.
  const scaleY = Math.max(0.0001, Math.hypot(r01, r11));
  const relativeDeterminant = r00 * r11 - r01 * r10;
  const scaleX = relativeDeterminant / scaleY;
  return {
    x: (d * dx - c * dy) / det,
    y: (-b * dx + a * dy) / det,
    rotation: Math.atan2(-r01, r11) * 180 / Math.PI,
    scaleX: Math.abs(scaleX) < 0.0001 ? 0.0001 : scaleX,
    scaleY
  };
}

function fillTextData(data, node, scaleX, scaleY) {
  data.text = node.characters || '';
  data.textAlignHorizontal = node.textAlignHorizontal || 'LEFT';
  data.textAlignVertical = node.textAlignVertical || 'TOP';
  data.textAutoResize = node.textAutoResize || 'NONE';
  const rawSize = node.fontSize === figma.mixed ? firstRangeValue(node, 'fontSize', 16) : finite(node.fontSize, 16);
  data.fontSize = rawSize * scaleY;
  const fontName = node.fontName === figma.mixed ? firstRangeValue(node, 'fontName', null) : node.fontName;
  if (fontName) { data.fontFamily = fontName.family || ''; data.fontStyle = fontName.style || ''; }
  data.color = getFirstSolidFill(node);
  data.lineHeight = metricPixels(node.lineHeight, rawSize) * scaleY;
  data.letterSpacing = metricPixels(node.letterSpacing, rawSize) * scaleX;
  const effectScale = Math.min(scaleX, scaleY);
  data.textShadows = getVisibleEffects(node)
    .filter(effect => effect.type === 'DROP_SHADOW')
    .map(effect => ({
      color: effect.color
        ? [effect.color.r, effect.color.g, effect.color.b, finite(effect.color.a, 1)]
        : [0, 0, 0, 0.25],
      offsetX: finite(effect.offset && effect.offset.x) * scaleX,
      offsetY: finite(effect.offset && effect.offset.y) * scaleY,
      radius: finite(effect.radius) * effectScale,
      spread: finite(effect.spread) * effectScale,
      blendMode: effect.blendMode || 'NORMAL'
    }));
}

function firstRangeValue(node, property, fallback) {
  if (!node.characters) return fallback;
  try {
    return property === 'fontName' ? node.getRangeFontName(0, 1) : node.getRangeFontSize(0, 1);
  } catch (_) { return fallback; }
}

function metricPixels(metric, fontSize) {
  if (!metric || metric === figma.mixed || metric.unit === 'AUTO') return 0;
  if (metric.unit === 'PIXELS') return finite(metric.value);
  if (metric.unit === 'PERCENT') return fontSize * finite(metric.value) / 100;
  return 0;
}

function hasDirectPaintOrStroke(node) {
  const fills = 'fills' in node && node.fills !== figma.mixed && Array.isArray(node.fills) &&
    node.fills.some(paint => paint.visible !== false);
  const strokes = 'strokes' in node && Array.isArray(node.strokes) &&
    node.strokes.some(paint => paint.visible !== false);
  return fills || strokes;
}

function hasVisibleEffects(node) {
  return getVisibleEffects(node).length > 0;
}

function getVisibleEffects(node) {
  return 'effects' in node && node.effects !== figma.mixed && Array.isArray(node.effects)
    ? node.effects.filter(effect => effect.visible !== false)
    : [];
}

function appendEffects(node, extraEffects) {
  if (!Array.isArray(extraEffects) || extraEffects.length === 0 || !('effects' in node)) return;
  const existing = node.effects !== figma.mixed && Array.isArray(node.effects)
    ? node.effects.filter(effect => effect.visible !== false)
    : [];
  try { node.effects = existing.concat(extraEffects); } catch (_) {}
}

function findFullBoundsEffectHost(node) {
  if (!('children' in node)) return null;
  const tolerance = 0.76;
  for (const child of node.children) {
    if (child.visible === false || child.type === 'TEXT' || child.type === 'SLICE') continue;
    const geometry = getLocalGeometry(child, node);
    const width = finite(child.width) * Math.abs(geometry.scaleX);
    const height = finite(child.height) * Math.abs(geometry.scaleY);
    if (Math.abs(geometry.x) <= tolerance && Math.abs(geometry.y) <= tolerance &&
        Math.abs(geometry.rotation) <= 0.1 &&
        Math.abs(width - finite(node.width)) <= tolerance &&
        Math.abs(height - finite(node.height)) <= tolerance)
      return child.id;
  }
  return null;
}

function getTintableMonochromeGradient(node, effectiveEffects) {
  if (node.type !== 'RECTANGLE' || !('fills' in node) || node.fills === figma.mixed ||
      !Array.isArray(node.fills) || (effectiveEffects && effectiveEffects.length > 0)) return null;
  const visible = node.fills.filter(paint => paint.visible !== false);
  if (visible.length !== 1 || !visible[0].type.startsWith('GRADIENT_')) return null;
  if ('strokes' in node && Array.isArray(node.strokes) &&
      node.strokes.some(paint => paint.visible !== false)) return null;
  if (getCornerRadius(node) > 0.01) return null;

  const paint = visible[0];
  const stops = Array.isArray(paint.gradientStops) ? paint.gradientStops : [];
  if (stops.length < 2) return null;

  // Find the brightest visible stop. Every other visible stop must be the same
  // hue multiplied by a scalar; alpha may vary independently.
  const visibleStops = stops.filter(stop => stop.color && finite(stop.color.a, 1) > 0.001);
  if (visibleStops.length === 0) return null;
  let tint = visibleStops[0].color;
  let tintPeak = Math.max(tint.r, tint.g, tint.b);
  for (const stop of visibleStops.slice(1)) {
    const peak = Math.max(stop.color.r, stop.color.g, stop.color.b);
    if (peak > tintPeak) {
      tint = stop.color;
      tintPeak = peak;
    }
  }

  let dominant = 0;
  if (tint.g > tint.r && tint.g >= tint.b) dominant = 1;
  else if (tint.b > tint.r && tint.b > tint.g) dominant = 2;
  const tintChannels = [tint.r, tint.g, tint.b];
  const normalizedStops = [];
  for (const stop of stops) {
    const source = stop.color;
    if (!source) return null;
    const alpha = finite(source.a, 1);
    let intensity = 1;
    if (alpha > 0.001 && tintPeak > 0.001) {
      const channels = [source.r, source.g, source.b];
      intensity = channels[dominant] / tintChannels[dominant];
      if (intensity < -0.001 || intensity > 1.001) return null;
      for (let channel = 0; channel < 3; channel++) {
        if (Math.abs(channels[channel] - tintChannels[channel] * intensity) > 0.01) return null;
      }
    } else if (alpha > 0.001 && tintPeak <= 0.001) {
      if (Math.max(source.r, source.g, source.b) > 0.01) return null;
    }
    intensity = Math.max(0, Math.min(1, intensity));
    normalizedStops.push({
      position: stop.position,
      color: { r: intensity, g: intensity, b: intensity, a: alpha }
    });
  }

  const normalizedPaint = Object.assign({}, paint, { gradientStops: normalizedStops });
  return {
    paint: normalizedPaint,
    tint: [tint.r, tint.g, tint.b, 1]
  };
}

function getAssetReuseKey(node, effectiveEffects) {
  if (!('fills' in node) || node.fills === figma.mixed || !Array.isArray(node.fills) ||
      (effectiveEffects && effectiveEffects.length > 0)) return '';
  const visible = node.fills.filter(paint => paint.visible !== false);
  if (visible.length !== 1 || visible[0].type !== 'IMAGE' || !visible[0].imageHash) return '';
  if ('strokes' in node && Array.isArray(node.strokes) &&
      node.strokes.some(paint => paint.visible !== false)) return '';
  if (getCornerRadius(node) > 0.01) return '';

  const paint = visible[0];
  // Size is deliberately excluded. Crop/transform/filter settings remain in
  // the key so only the same Figma image presentation can share one Unity asset.
  return 'image:' + JSON.stringify({
    hash: paint.imageHash,
    scaleMode: paint.scaleMode || 'FILL',
    transform: paint.imageTransform || null,
    rotation: finite(paint.rotation),
    filters: paint.filters || null,
    opacity: paint.opacity === undefined ? 1 : paint.opacity,
    blendMode: paint.blendMode || 'NORMAL'
  });
}

function getImageScaleMode(node) {
  if (!('fills' in node) || node.fills === figma.mixed || !Array.isArray(node.fills)) return '';
  const paint = node.fills.find(fill => fill && fill.visible !== false && fill.type === 'IMAGE');
  return paint && typeof paint.scaleMode === 'string' ? paint.scaleMode : '';
}

function getSimpleSolidFill(node) {
  if (node.type !== 'RECTANGLE' || !('fills' in node) || node.fills === figma.mixed || !Array.isArray(node.fills)) return null;
  const visible = node.fills.filter(paint => paint.visible !== false);
  if (visible.length !== 1 || visible[0].type !== 'SOLID') return null;
  if ('strokes' in node && Array.isArray(node.strokes) && node.strokes.some(paint => paint.visible !== false)) return null;
  if (hasVisibleEffects(node)) return null;
  if ('cornerRadius' in node && node.cornerRadius !== figma.mixed && finite(node.cornerRadius) > 0) return null;
  const paint = visible[0];
  return [paint.color.r, paint.color.g, paint.color.b, paint.opacity === undefined ? 1 : paint.opacity];
}

function getFirstSolidFill(node) {
  if (!('fills' in node) || node.fills === figma.mixed || !Array.isArray(node.fills)) return [1, 1, 1, 1];
  const paint = node.fills.find(value => value.visible !== false && value.type === 'SOLID');
  return paint ? [paint.color.r, paint.color.g, paint.color.b, paint.opacity === undefined ? 1 : paint.opacity] : [1, 1, 1, 1];
}

function getCornerRadius(node) {
  if (!('cornerRadius' in node)) return 0;
  if (node.cornerRadius !== figma.mixed) return finite(node.cornerRadius);
  return Math.max(...['topLeftRadius', 'topRightRadius', 'bottomRightRadius', 'bottomLeftRadius']
    .map(key => key in node ? finite(node[key]) : 0));
}

function copyProperty(source, target, property) {
  try {
    if (property in source && source[property] !== figma.mixed && source[property] !== undefined)
      target[property] = source[property];
  } catch (_) {}
}

function finite(value, fallback) {
  return Number.isFinite(value) ? value : (fallback === undefined ? 0 : fallback);
}

function bytesToBase64(bytes) {
  const chunkSize = 0x8000;
  let binary = '';
  for (let i = 0; i < bytes.length; i += chunkSize) {
    const chunk = bytes.subarray(i, Math.min(i + chunkSize, bytes.length));
    binary += String.fromCharCode.apply(null, chunk);
  }
  return btoa(binary);
}
