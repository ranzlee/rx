(()=>{
	if (typeof Idiomorph === 'undefined') {
		console.error("Idiomorph is undefined. Please make sure the idiomorph library is included. It can be downloaded here -> https://github.com/bigskysoftware/idiomorph/blob/main/dist/idiomorph.min.js. Using 'outerHTML' instead.");
		return;
	}
	if(document.__fixi_mo) return;
	document.__fixi_mo = new MutationObserver((recs)=>recs.forEach((r)=>r.type === "childList" && r.addedNodes.forEach((n)=>process(n))))
	let send = (elt, type, detail, bub)=>elt.dispatchEvent(new CustomEvent("fx:" + type, {detail, cancelable:true, bubbles:bub !== false, composed:true}))
	let attr = (elt, name, defaultVal)=>elt.getAttribute(name) || defaultVal
	let ignore = (elt)=>elt.closest("[fx-ignore]") != null
	let init = (elt)=>{
		let options = {}
		if (elt.__fixi || ignore(elt) || !send(elt, "init", {options})) return
		elt.__fixi = async(evt)=>{
			let reqs = elt.__fixi.requests ||= new Set()
			let form = elt.form || elt.closest("form")
			let body = new FormData(form ?? undefined, evt.submitter)
			if (!form && elt.name) body.append(elt.name, elt.value)
			let ac = new AbortController()
			let cfg = {
				trigger:evt,
				action:attr(elt, "fx-action"),
				method:attr(elt, "fx-method", "GET").toUpperCase(),
				target:null,
				swap:null,
				body,
				drop:reqs.size,
				headers:{"FX-Request":"true"},
				abort:ac.abort.bind(ac),
				signal:ac.signal,
				preventTrigger:true,
				transition:document.startViewTransition?.bind(document),
				fetch:fetch.bind(window)
			}
			let go = send(elt, "config", {cfg, requests:reqs})
			if (cfg.preventTrigger) evt.preventDefault()
			if (!go || cfg.drop) return
			if (/GET|DELETE/.test(cfg.method)){
				let params = new URLSearchParams(cfg.body)
				if (params.size)
					cfg.action += (/\?/.test(cfg.action) ? "&" : "?") + params
				cfg.body = null
			}
			reqs.add(cfg)
			try {
				if (cfg.confirm){
					let result = await cfg.confirm()
					if (!result) return
				}
				if (!send(elt, "before", {cfg, requests:reqs})) return
				cfg.response = await cfg.fetch(cfg.action, cfg)
				cfg.text = await cfg.response.text()
				if (!send(elt, "after", {cfg})) return
			} catch(error) {
				send(elt, "error", {cfg, error})
				return
			} finally {
				reqs.delete(cfg)
				send(elt, "finally", {cfg})
			}
			let doSwap = ()=>{
				if (cfg.swap instanceof Function)
					return cfg.swap(cfg)
				else if (/(before|after)(begin|end)/.test(cfg.swap))
					cfg.target.insertAdjacentHTML(cfg.swap, cfg.text)
				else if(cfg.swap in cfg.target)
					cfg.target[cfg.swap] = cfg.text
				else throw cfg.swap
			}
			if (cfg.transition)
				await cfg.transition(doSwap).finished
			else
				await doSwap()
			send(elt, "swapped", {cfg})
			if (!document.contains(elt)) send(document, "swapped", {cfg})
		}
		elt.__fixi.evt = attr(elt, "fx-trigger", elt.matches("form") ? "submit" : elt.matches("input:not([type=button]),select,textarea") ? "change" : "click")
		elt.addEventListener(elt.__fixi.evt, elt.__fixi, options)
		send(elt, "inited", {}, false)
	}
	let process = (n)=>{
		if (n.matches){
			if (ignore(n)) return
			if (n.matches("[fx-action]")) init(n)
		}
		if(n.querySelectorAll) n.querySelectorAll("[fx-action]").forEach(init)
	}
	document.addEventListener("fx:process", (evt) => { 
		process(evt.target) 
	})
	document.addEventListener("DOMContentLoaded", ()=>{
		document.__fixi_mo.observe(document.documentElement, {childList:true, subtree:true})
		process(document.body)
	})
})()

function addAntiforgeryCookieToRequest(evt) {
	const value = `; ${document.cookie}`;
	const parts = value.split("; RequestVerificationToken=");
	if (parts.length !== 2) {
		return;
	}
	evt.detail.cfg.headers["RequestVerificationToken"] = parts
		.pop()
		.split(";")
		.shift();
}

function encodeBodyAsJson(evt) {
	evt.detail.cfg.headers['Content-Type'] = 'application/json';
	const object = {};
	evt.detail.cfg.body.forEach(function(value, key) {
		if (Object.hasOwn(object, key)) {
			if (!Array.isArray(object[key])) {
				object[key] = [object[key]];
			}
			object[key].push(value);
		} else {
			object[key] = value;
		}
	})
	evt.detail.cfg.body = JSON.stringify(object);
}

function morphTarget(target, node) {
	Idiomorph.morph(target, node, { morphStyle: "outerHTML" }).forEach(n => {
		n.dispatchEvent(new CustomEvent("fx:process", { bubbles: true }));
	});
}

function morphFragments(cfg) {
	const parser = new DOMParser();
	const doc = parser.parseFromString(cfg.text, 'text/html');
	//append new head elements
	if (doc.head.children.length > 0) {
		Idiomorph.morph(document.head, doc.head, {head: {style: 'append'}})
	}
	//morph body elements
	doc.body.childNodes.forEach(node => {
		if (node.nodeType !== Node.ELEMENT_NODE) {
			return;
		}
		var target = document.getElementById(node.id);
		if (target === null) {
			return;
		}
		morphTarget(target, node);
	});
}

function replaceFragments(cfg) {
	const parser = new DOMParser();
	const doc = parser.parseFromString(cfg.text, 'text/html');
	//append new head elements
	if (doc.head.children.length > 0) {
		Idiomorph.morph(document.head, doc.head, {head: {style: 'append'}})
	}
	//replace body elements
	const bodyNodes = Array.from(doc.body.childNodes);
	bodyNodes.forEach(node => {
		if (node.nodeType !== Node.ELEMENT_NODE) {
			return;
		}
		var target = document.getElementById(node.id);
		if (target === null) {
			return;
		}
		//target.replaceWith(node);
		target["outerHTML"] = node.outerHTML;
		node.dispatchEvent(new CustomEvent("fx:process", { bubbles: true }));
	});
}

document.addEventListener("fx:config", evt => {
	addAntiforgeryCookieToRequest(evt);
	encodeBodyAsJson(evt);
});

document.addEventListener("fx:after", evt => {
	const cfg = evt.detail.cfg;
	//custom behaviors
	if (cfg.response.status === 204) {
		// don't swap on no-content
		evt.preventDefault();
		return;
	}
	if (cfg.method === "DELETE" && cfg.response.status === 200) {
		//TODO: remove target element based on {inner}|{outer}HTML
		evt.preventDefault();
		return;
	}
	//check for client redirect
	const redirect = cfg.response.headers.get("fx-redirect");
	if (redirect) {
		evt.preventDefault();
		window.location.assign(redirect);
		return;
	}
	//swap processing
	// const target = cfg.response.headers.get("fx-target");
	// if (target) {
	// 	var ele = document.querySelector(val);
	// 	if (!ele) {
	// 		console.error(`The target ${target} is not an element.`);
	// 		return;
	// 	}
	// 	cfg.target = ele;
	// }
	const swap = cfg.response.headers.get("fx-swap");
	if (swap === "replace") {
		cfg.swap = (cfg) => replaceFragments(cfg);
		return;
	}
	if (swap === "morph") {
		cfg.swap = (cfg) => morphFragments(cfg);
		return;
	}
});

