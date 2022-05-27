import * as constants from './constants'
import * as headerdef from './frameheaderdef'
import { HeaderField } from './frameheaderdef'
import {
	getDecoder, getEncoder, callFunction,
	extend, mergeArrayBuffer
} from './utilities'

export class ConnectionOptions {
	url: string = '';
	urlList: string[] = [];
	rid: string = '0';
	aid: string = '0';
	uid: string = '0';
	from: string = '-1';
	retry: boolean = true;
	retryMaxCount: number = 0;
	retryInterval: number = 5;
	retryThreadCount: number = 10;
	connectTimeout: number = 5e3;
	retryConnectCount: number = 3;
	retryconnectTimeout: number = 1e4;
	retryRoundInterval: number = Math.floor(2 * Math.random()) + 3;
	customAuthParam: { key: string, type: "string" | "boolean" | "number", value: string }[] = [];
	fallback: Function;
	heartBeatInterval: number = 30;
	onReceivedMessage: Function;
	onReceiveAuthRes: Function;
	onHeartBeatReply: Function;
	onInitialized: Function;
	onOpen: Function;
	onClose: Function;
	onError: Function;
	onListConnectError: Function;
	onRetryFallback: Function;
};

export class AuthInfo { origin: object; encode: ArrayBuffer }

class ConnectionState {
	retryCount: number = 0;
	listConnectFinishedCount: number = 0;
	index: number = 0;
	connectTimeoutTimes: number = 0;
}

type CallbackQueues = {
	onInitializedQueue: Function[];
	onOpenQueue: Function[];
	onCloseQueue: Function[];
	onErrorQueue: Function[];
	onReceivedMessageQueue: Function[];
	onHeartBeatReplyQueue: ((e: PopularityMessage) => void)[];
	onRetryFallbackQueue: Function[];
	onListConnectErrorQueue: Function[];
	onReceiveAuthResQueue: Function[];
};

type HandshakeV3 = {
	uid: number;
	roomid: number;
	protover: number;
	aid?: number;
	from?: number;
};

interface Message {
	cmd: string;
}

interface PopularityMessage {
	count: number;
}

interface Packet {
	body: Packet[] | Message | PopularityMessage;
	packetLen: number;
	op: number;
	ver: number;
	seq?: number;
};

export class Connection {
	options: ConnectionOptions;
	wsBinaryHeaderList: HeaderField[];
	authInfo: AuthInfo;
	state: ConnectionState;
	callbackQueueList: CallbackQueues;
	HEART_BEAT_INTERVAL: number;
	CONNECT_TIMEOUT: number;
	ws: WebSocket;
	encoder: TextEncoder;
	decoder: TextDecoder;
	constructor(opt: any) {
		if (Connection.checkOptions(opt)) {
			var defaultopt = new ConnectionOptions;
			// n[5].a.extend
			this.options = extend<ConnectionOptions>(defaultopt, opt);
			this.wsBinaryHeaderList = extend<HeaderField[]>([], headerdef);
			this.authInfo = new AuthInfo;
			if (this.options.urlList.length !== 0 &&
				this.options.retryMaxCount !== 0 &&
				this.options.retryMaxCount < this.options.urlList.length) {
				this.options.retryMaxCount =
					this.options.urlList.length - 1;
			}
			this.state = new ConnectionState;
			this.callbackQueueList = {
				onInitializedQueue: [],
				onOpenQueue: [],
				onCloseQueue: [],
				onErrorQueue: [],
				onReceivedMessageQueue: [],
				onHeartBeatReplyQueue: [],
				onRetryFallbackQueue: [],
				onListConnectErrorQueue: [],
				onReceiveAuthResQueue: [],
			};
			this.HEART_BEAT_INTERVAL = 0;
			this.CONNECT_TIMEOUT = 0;
			this.mixinCallback().initialize(
				this.options.urlList.length > 0
					? this.options.urlList[0]
					: this.options.url
			);
		}
	}
	initialize(e: string | URL) {
		var t = this;
		var opt = this.options;
		try {
			this.ws = new WebSocket(e.toString());
			this.ws.binaryType = 'arraybuffer';
			this.ws.onopen = this.onOpen.bind(this);
			this.ws.onmessage = this.onMessage.bind(this);
			this.ws.onclose = this.onClose.bind(this);
			this.ws.onerror = this.onError.bind(this);
			callFunction(this.callbackQueueList.onInitializedQueue);
			this.callbackQueueList.onInitializedQueue = [];
			var r = this.state.connectTimeoutTimes >= 3
				? opt.retryconnectTimeout
				: opt.connectTimeout;
			this.CONNECT_TIMEOUT = setTimeout(function () {
				t.state.connectTimeoutTimes += 1;
				console.error(
					'connect timeout ',
					t.state.connectTimeoutTimes
				);
				t.ws.close();
			}, r);
		} catch (e) {
			if (typeof opt.fallback == 'function') {
				opt.fallback();
			}
		}
		return this;
	}
	onOpen() {
		callFunction(this.callbackQueueList.onOpenQueue);
		this.state.connectTimeoutTimes = 0;
		if (this.CONNECT_TIMEOUT) {
			clearTimeout(this.CONNECT_TIMEOUT);
		}
		this.userAuthentication();
		return this;
	}
	userAuthentication() {
		var opt = this.options;

		var authOrigin: HandshakeV3 =
		{
			uid: parseInt(opt.uid, 10),
			roomid: parseInt(opt.rid, 10),
			protover: 3,
		};
		if (opt.aid) {
			authOrigin.aid = parseInt(opt.aid, 10);
		}
		if (parseInt(opt.from, 10) > 0) {
			authOrigin.from = parseInt(opt.from, 10) || 7;
		}
		for (var i = 0; i < opt.customAuthParam.length; i++) {
			var param = opt.customAuthParam[i];
			var paramtype = param.type || 'string';
			switch ((authOrigin[param.key] !== undefined &&
				console.error(
					'Token has the same key already! \u3010' +
					param.key +
					'\u3011'
				),
				(param.key.toString() && param.value.toString()) ||
				console.error(
					'Invalid customAuthParam, missing key or value! \u3010' +
					param.key +
					'\u3011-\u3010' +
					param.value +
					'\u3011'
				),
				paramtype)) {
				case 'string':
					authOrigin[param.key] = param.value;
					break;
				case 'number':
					authOrigin[param.key] = parseInt(param.value, 10);
					break;
				case 'boolean':
					authOrigin[param.key] = !!authOrigin[param.value];
					break;
				default:
					console.error(
						'Unsupported customAuthParam type!\u3010' +
						paramtype +
						'\u3011'
					);
					return;
			}
		}
		var encoded = this.convertToArrayBuffer(
			JSON.stringify(authOrigin),
			constants.WS_OP_USER_AUTHENTICATION
		);
		this.authInfo.origin = authOrigin;
		this.authInfo.encode = encoded;
		setTimeout(function () {
			this.ws.send(encoded);
		}, 0);
	}
	getAuthInfo() {
		return this.authInfo;
	}
	heartBeat() {
		var e = this;
		clearTimeout(this.HEART_BEAT_INTERVAL);
		var t = this.convertToArrayBuffer({}, constants.WS_OP_HEARTBEAT);
		this.ws.send(t);
		this.HEART_BEAT_INTERVAL = setTimeout(function () {
			e.heartBeat();
		}, 1e3 * this.options.heartBeatInterval);
	}

	static isMessage(m: any): m is Packet {
		return "body" in m && typeof m.body === "object" &&
			"packetLen" in m && typeof m.packetLen === "number" &&
			"op" in m && typeof m.op === "number" &&
			"ver" in m && typeof m.ver === "number";
	}

	onMessage(e: Packet | Packet[] | MessageEvent<ArrayBuffer>) {
		var t = this;
		try {
			var n: typeof e;

			if (e instanceof MessageEvent)
				n = this.convertToObject(e.data);
			if (n instanceof Array) {
				n.forEach(function (e: Packet) {
					t.onMessage(e);
				});
			} else if (Connection.isMessage(n)) {
				switch (n.op) {
					case constants.WS_OP_HEARTBEAT_REPLY:
						this.onHeartBeatReply(n.body as PopularityMessage);
						break;
					case constants.WS_OP_MESSAGE:
						this.onMessageReply(n.body, n.seq);
						break;
					case constants.WS_OP_CONNECT_SUCCESS:
						if ((n.body as { code?: number }[]).length !== 0 && n.body[0]?.code !== void 0) {
							switch (n.body[0].code) {
								case constants.WS_AUTH_OK:
									this.heartBeat();
									break;
								case constants.WS_AUTH_TOKEN_ERROR:
									this.options.retry = false;
									if (typeof this.options.onReceiveAuthRes ==
										'function') {
										this.options.onReceiveAuthRes(n.body);
									}
									break;
								default:
									this.onClose();
							}
						} else {
							this.heartBeat();
						}
				}
			}
		} catch (e) {
			console.error('WebSocket Error: ', e);
		}
		return this;
	}
	onMessageReply(e: Function[] | object, t: number) {
		var n = this;
		try {
			if (e instanceof Array) {
				e.forEach(function (e) {
					n.onMessageReply(e, t);
				});
			} else if (e instanceof Object &&
				typeof this.options.onReceivedMessage == 'function') {
				this.options.onReceivedMessage(e, t);
			}
		} catch (e) {
			console.error('On Message Resolve Error: ', e);
		}
	}
	onHeartBeatReply(e: PopularityMessage) {
		callFunction(
			this.callbackQueueList.onHeartBeatReplyQueue,
			e
		);
	}
	onClose(e?: { code: number; }) {
		var t = this;
		if (e.code > 1001) {
			callFunction(this.callbackQueueList.onErrorQueue, e);
		}
		var n = this.options.urlList.length;
		callFunction(this.callbackQueueList.onCloseQueue);
		clearTimeout(this.HEART_BEAT_INTERVAL);
		if (this.options.retry) {
			if (this.checkRetryState()) {
				setTimeout(function () {
					console.error(
						'Danmaku Websocket Retry .',
						t.state.retryCount
					);
					t.state.index += 1;
					if (n === 0 ||
						t.state.retryCount > t.options.retryThreadCount) {
						setTimeout(function () {
							t.initialize(t.options.url);
						}, 1e3 * t.options.retryRoundInterval);
					} else if (n !== 0 && t.state.index > n - 1) {
						t.state.index = 0;
						t.state.listConnectFinishedCount += 1;
						if (t.state.listConnectFinishedCount === 1) {
							callFunction(
								t.callbackQueueList.onListConnectErrorQueue
							);
						}
						setTimeout(function () {
							t.initialize(t.options.urlList[t.state.index]);
						}, 1e3 * t.options.retryRoundInterval);
					} else {
						t.initialize(t.options.urlList[t.state.index]);
					}
				}, 1e3 * this.options.retryInterval);
			} else {
				console.error('Danmaku Websocket Retry Failed.');
				callFunction(
					this.callbackQueueList.onRetryFallbackQueue
				);
			}
			return this;
		} else {
			return this;
		}
	}
	onError(e: any) {
		console.error('Danmaku Websocket On Error.', e);
		return this;
	}
	destroy() {
		if (this.HEART_BEAT_INTERVAL) {
			clearTimeout(this.HEART_BEAT_INTERVAL);
		}
		if (this.CONNECT_TIMEOUT) {
			clearTimeout(this.CONNECT_TIMEOUT);
		}
		this.options.retry = false;
		if (this.ws) {
			this.ws.close();
		}
		this.ws = null;
	}
	/**
	 * serialize
	 */
	convertToArrayBuffer(payload: any, opcode: number): ArrayBuffer {
		if (!this.encoder) {
			/** @type {TextEncoder} */
			this.encoder = getEncoder();
		}

		//#region emit frame header
		var buff = new ArrayBuffer(constants.WS_PACKAGE_HEADER_TOTAL_LENGTH);
		var writer = new DataView(buff, constants.WS_PACKAGE_OFFSET);
		var encoded = this.encoder.encode(payload);
		writer.setInt32(
			constants.WS_PACKAGE_OFFSET,
			constants.WS_PACKAGE_HEADER_TOTAL_LENGTH + encoded.byteLength
		);
		this.wsBinaryHeaderList[2].value = opcode;
		this.wsBinaryHeaderList.forEach(function (fields) {
			if (fields.bytes === 4) {
				writer.setInt32(fields.offset, fields.value);
			} else if (fields.bytes === 2) {
				writer.setInt16(fields.offset, fields.value);
			}
		});
		//#endregion
		return mergeArrayBuffer(buff, encoded);
	}
	/**
	 * deserialize
	 */
	convertToObject(buffer: ArrayBuffer): Packet {
		var reader = new DataView(buffer);
		var ret: Partial<Packet> = { body: [] };
		ret.packetLen = reader.getInt32(constants.WS_PACKAGE_OFFSET);
		//#region parse header
		this.wsBinaryHeaderList.forEach(function (fielddef) {
			if (fielddef.bytes === 4) {
				ret[fielddef.key] = reader.getInt32(fielddef.offset);
			} else if (fielddef.bytes === 2) {
				ret[fielddef.key] = reader.getInt16(fielddef.offset);
			}
		});
		//#endregion
		if (ret.packetLen < buffer.byteLength) {
			this.convertToObject(buffer.slice(0, ret.packetLen));
		}
		if (!this.decoder) {
			/** @type {TextDecoder} */
			this.decoder = getDecoder();
		}

		if (!ret.op ||
			(constants.WS_OP_MESSAGE !== ret.op &&
				ret.op !== constants.WS_OP_CONNECT_SUCCESS)) {
			if (ret.op && constants.WS_OP_HEARTBEAT_REPLY === ret.op) {
				ret.body = {
					count: reader.getInt32(constants.WS_PACKAGE_HEADER_TOTAL_LENGTH),
				};
			}
		} else {
			var currentPos = constants.WS_PACKAGE_OFFSET;
			/** @type {number} */
			var currentPacketLen: number = ret.packetLen;
			/** @type {number} */
			var currentHeaderLen: number = 0;
			for (var parsed: Packet[]; currentPos < buffer.byteLength; currentPos += currentPacketLen) {
				currentPacketLen = reader.getInt32(currentPos);
				currentHeaderLen = reader.getInt16(currentPos + constants.WS_HEADER_OFFSET);
				try {
					if (ret.ver === constants.WS_BODY_PROTOCOL_VERSION_NORMAL) {
						var jsonobj = this.decoder.decode(buffer.slice(currentPos + currentHeaderLen, currentPos + currentPacketLen));
						parsed = jsonobj.length !== 0 ? JSON.parse(jsonobj) : null;
					} else if (ret.ver === constants.WS_BODY_PROTOCOL_VERSION_BROTLI) {
						var realJson = buffer.slice(currentPos + currentHeaderLen, currentPos + currentPacketLen);
						function BrotliDecode(buff: Uint8Array): { buffer: ArrayBuffer; } { throw null };
						var decoded = BrotliDecode(new Uint8Array(realJson));
						parsed = this.convertToObject(decoded.buffer).body as Packet[];
					}
					if (parsed) {
						(ret.body as Packet[]).push(...parsed);
					}
				} catch (e) {
					console.error(
						'decode body error:',
						new Uint8Array(buffer),
						ret,
						e
					);
				}
			}
		}
		return ret as Packet;
	}
	send(e: string | ArrayBufferLike | Blob | ArrayBufferView) {
		if (this.ws) {
			this.ws.send(e);
		}
	}
	addCallback(callback: Function, queue: any[]) {
		if (typeof callback == 'function' && queue instanceof Array) {
			queue.push(callback);
		}
		return this;
	}
	mixinCallback() {
		var opt = this.options;
		var queues = this.callbackQueueList;
		this.addCallback(
			opt.onReceivedMessage,
			queues.onReceivedMessageQueue
		)
			.addCallback(opt.onHeartBeatReply, queues.onHeartBeatReplyQueue)
			.addCallback(opt.onInitialized, queues.onInitializedQueue)
			.addCallback(opt.onOpen, queues.onOpenQueue)
			.addCallback(opt.onClose, queues.onCloseQueue)
			.addCallback(opt.onError, queues.onErrorQueue)
			.addCallback(opt.onRetryFallback, queues.onRetryFallbackQueue)
			.addCallback(
				opt.onListConnectError,
				queues.onListConnectErrorQueue
			)
			.addCallback(opt.onReceiveAuthRes, queues.onReceiveAuthResQueue);
		return this;
	}
	getRetryCount() {
		return this.state.retryCount;
	}
	checkRetryState() {
		var e = this.options;
		var t = false;
		if (e.retryMaxCount === 0 ||
			this.state.retryCount < e.retryMaxCount) {
			this.state.retryCount += 1;
			t = true;
		}
		return t;
	}
	static checkOptions(opt: { url: any; rid: any; }) {
		if (opt || opt instanceof Object) {
			if (opt.url) {
				return (
					!!opt.rid ||
					(console.error(
						'WebSocket Initialize options rid(cid) missing.'
					),
						false)
				);
			} else {
				console.error(
					'WebSocket Initialize options url missing.'
				);
				return false;
			}
		} else {
			console.error(
				'WebSocket Initialize options missing or error.',
				opt
			);
			return false;
		}
	}
}