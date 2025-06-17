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
				merge:null,
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
			let doMerge = ()=>{
				if (cfg.merge instanceof Function)
					return cfg.merge(cfg)
				else if (/(before|after)(begin|end)/.test(cfg.merge))
					cfg.target.insertAdjacentHTML(cfg.merge, cfg.text)
				else if(cfg.merge in cfg.target)
					cfg.target[cfg.merge] = cfg.text
				else throw cfg.merge
			}
			if (cfg.transition)
				await cfg.transition(doMerge).finished
			else
				await doMerge()
			send(elt, "merged", {cfg})
			if (!document.contains(elt)) send(document, "merged", {cfg})
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

function normalizeScriptTags(fragment) {
	Array.from(fragment.querySelectorAll('script')).forEach(script => {
      	const newScript = duplicateScript(script);
    	const parent = script.parentNode;
        parent.insertBefore(newScript, script);
        script.remove();
    });
}

function duplicateScript(script) {
    const newScript = document.createElement('script');
	Array.from(script.attributes).forEach(attr => {
		newScript.setAttribute(attr.name, attr.value);
	});
    newScript.textContent = script.textContent;
    newScript.async = false;
    return newScript;
}

function mergeFragments(cfg) {
	const merge = cfg.response.headers.get("fx-merge");
	if (!merge) {
		console.error('Expected a "fx-merge" header object.');
		return;
	}
	const mergeStrategyArray = JSON.parse(merge);
	const parser = new DOMParser();
	const doc = parser.parseFromString('<body><template>' + cfg.text + '</template></body>', 'text/html');
	const template = doc.body.querySelector('template').content;
	const fragments = Array.from(template.childNodes);
	fragments.forEach(node => {
		if (node.nodeType !== Node.ELEMENT_NODE) {
			return;
		}
		//adjacent element merge
		if (node instanceof HTMLTemplateElement) {
			return;
		}
		//swap or morph
		var target = document.getElementById(node.id);
		if (target === null) {
			console.error(`Expected a DOM element with id ${node.id}.`);
			return;
		}
		var mergeStrategy = mergeStrategyArray.find(s => s.target === target.id);
		if (!mergeStrategy) {
			console.error(`Expected a merge strategy item with id ${target.id}.`);
			return;
		}
		//morph
		if (mergeStrategy.strategy === 'morph') {
			const ignoreActive = cfg.response.headers.get("fx-morph-ignore-active") === "True";
			Idiomorph.morph(target, node, { morphStyle: "outerHTML", ignoreActiveValue: ignoreActive }).forEach(n => {
				normalizeScriptTags(n);
				n.dispatchEvent(new CustomEvent("fx:process", { bubbles: true }));
			});
			return;
		}
		//swap
		normalizeScriptTags(node);
		target.replaceWith(node);
		target.dispatchEvent(new CustomEvent("fx:process", { bubbles: true }));
	});
}

document.addEventListener("fx:config", evt => {
	addAntiforgeryCookieToRequest(evt);
	encodeBodyAsJson(evt);
});

document.addEventListener("fx:after", evt => {
	const cfg = evt.detail.cfg;
	//check for client redirect
	const redirect = cfg.response.headers.get("fx-redirect");
	if (redirect) {
		evt.preventDefault();
		window.location.assign(redirect);
		return;
	}
	//redirect and error response processing
	if (cfg.response.status >= 300 && cfg.response.status < 400) {
		evt.preventDefault();
		console.warn("A client redirect was issued on a fetch request. Please set the fx-redirect header to redirect async requests to the appropriate route.")
		return;
	}
	if (cfg.response.status >= 400 && cfg.response.status < 500) {
		evt.preventDefault();
		console.error("A client error was issued on a fetch request. Please set the fx-redirect header to redirect async requests to an appropriate error route.")
		return;
	}
	if (cfg.response.status >= 500) {
		evt.preventDefault();
		console.error("A server error was issued on a fetch request. Please set the fx-redirect header to redirect async requests to an appropriate error route..")
		return;
	}
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
	cfg.merge = (cfg) => mergeFragments(cfg);
});

document.addEventListener("fx:error", evt => {
	console.error(evt);
});

