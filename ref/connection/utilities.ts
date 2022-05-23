// utilities
function getDecoder(): TextDecoder {
	return new TextDecoder();
}
function getEncoder(): TextEncoder {
	return new TextEncoder();
}
function mergeArrayBuffer(e:ArrayBuffer, t:ArrayBuffer): ArrayBuffer {
	var n = new Uint8Array(e);
	var o = new Uint8Array(t);
	var r = new Uint8Array(n.byteLength + o.byteLength);
	r.set(n, 0);
	r.set(o, n.byteLength);
	return r.buffer;
}
function callFunction(e: Array<Function> | Function, ...t:any) {
	if (e instanceof Array && e.length) {
		e.forEach(function (e) {
			return typeof e == 'function' && e(t);
		});
		return null;
	} else {
		return typeof e == 'function' && e(t);
	}
}
function extend<T>(source: T, ...args:any) : T {
	var t = args.length;
	var bases = Array(t > 1 ? t - 1 : 0);
	for (var o = 1; o < t; o++) {
		bases[o - 1] = args[o];
	}
	var ret = source || {};
	if (ret instanceof Object) {
		bases.forEach(function (base) {
			if (base instanceof Object) {
				Object.keys(base).forEach(function (t) {
					ret[t] = base[t];
				});
			}
		});
	}
	return ret as T;
};

export { getEncoder, getDecoder, callFunction, mergeArrayBuffer, extend };