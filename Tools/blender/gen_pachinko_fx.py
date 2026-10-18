"""파칭코 액정 위에 얹는 터지는 이펙트 스프라이트 시퀀스 (Blender 렌더 + numpy 빛 번짐 후처리).

산출: ~/grd/GRD_pachinko_fx/<효과>/  add/f_0001.png (검정 배경 RGB — 더하기 합성용)
                                      rgba/f_0001.png (투명 PNG — 일반 합성용) · meta.json
렌더(Blender):   blender -b --factory-startup --python gen_pachinko_fx.py -- render <효과> [프레임 제한]
후처리(시스템 파이썬):  /usr/bin/python3 gen_pachinko_fx.py post <효과>
미리보기 시트:   /usr/bin/python3 gen_pachinko_fx.py sheet <효과…>
사장님 Blender 창(MCP):  g={'__name__':'pcfx'}; exec(open(...).read(), g); g['preview']('starburst', 4)

효과: starburst(섬광 0.4s) · shockwave(충격파 링 0.4s) · text_<이름>(예고 글자 1.2s: chance geki kakuhen push)
원리: 더하기 이펙트는 Emission+Transparent(=더하기 블렌드)로 검정 배경에 그리고, 후처리에서 빛 번짐을 더한 뒤
      alpha=max(rgb)·색=rgb/alpha 로 투명 PNG 도 만든다. 글자는 진짜 금속 셰이딩(투명 배경)+더하기 반짝이를 따로 렌더해 합친다.
"""
import sys, os, math, json, random

try:
    import bpy
    from mathutils import Vector, Matrix
    HAVE_BPY = True
except ImportError:
    HAVE_BPY = False

OUT = os.path.expanduser('~/grd/GRD_pachinko_fx')
FONT = os.path.expanduser('~/grd/GRD_pachinko_art/_fonts/DelaGothicOne-Regular.ttf')
W, H = 1920, 1080
FPS = 30


# =============================================================================
#  Blender 쪽
# =============================================================================
if HAVE_BPY:

    # ---- 장면 초기화 ---------------------------------------------------------
    def reset(transparent=False):
        for o in list(bpy.data.objects):
            bpy.data.objects.remove(o, do_unlink=True)
        for c in list(bpy.data.collections):
            bpy.data.collections.remove(c)
        for m in list(bpy.data.materials):
            bpy.data.materials.remove(m)
        for t in list(bpy.data.curves):
            bpy.data.curves.remove(t)
        sc = bpy.context.scene
        sc.frame_start, sc.frame_end = 1, 1
        sc.render.fps = FPS
        r = sc.render
        r.resolution_x, r.resolution_y, r.resolution_percentage = W, H, 100
        r.film_transparent = transparent
        r.image_settings.file_format = 'PNG'
        r.image_settings.color_mode = 'RGBA' if transparent else 'RGB'
        for eng in ('BLENDER_EEVEE_NEXT', 'BLENDER_EEVEE'):
            try:
                r.engine = eng
                break
            except TypeError:
                pass
        try:
            sc.eevee.taa_render_samples = 48
        except Exception:
            pass
        sc.view_settings.view_transform = 'Standard'
        sc.view_settings.look = 'None'
        w = bpy.data.worlds.get('fxw') or bpy.data.worlds.new('fxw')
        w.use_nodes = True
        w.node_tree.nodes.clear()
        bg = w.node_tree.nodes.new('ShaderNodeBackground')
        bg.inputs['Color'].default_value = (0, 0, 0, 1)
        o = w.node_tree.nodes.new('ShaderNodeOutputWorld')
        w.node_tree.links.new(bg.outputs['Background'], o.inputs['Surface'])
        sc.world = w
        return sc

    def coll(name):
        c = bpy.data.collections.get(name) or bpy.data.collections.new(name)
        if c.name not in bpy.context.scene.collection.children:
            bpy.context.scene.collection.children.link(c)
        return c

    def camera(persp=True, D=60.0):
        """원근(망원): y=0 평면 1920px = 19.2 BU. 정사영과 거의 같지만 글자 3D 회전이 입체로 보인다."""
        cd = bpy.data.cameras.new('cam')
        if persp:
            cd.sensor_fit = 'HORIZONTAL'
            cd.sensor_width = 36.0
            cd.lens = 36.0 * D / 19.2
        else:
            cd.type = 'ORTHO'
            cd.ortho_scale = 19.2
        cd.clip_end = 500
        co = bpy.data.objects.new('cam', cd)
        co.location = (0, -D, 0)
        co.rotation_euler = (math.radians(90), 0, 0)
        bpy.context.scene.collection.objects.link(co)
        bpy.context.scene.camera = co
        return co

    # ---- 더하기 그라데이션 재료 -----------------------------------------------
    def grad_mat(name, kind, stops, strength=2.0, power=1.0, noise=0.0, noise_scale=6.0, ring=None):
        """kind: spike(+X 방향 가늘게) · rad(둥근 후광) · ring(고리) · star(십자 반짝이)
        stops: [(위치, (r,g,b), 알파)] — spike는 길이 u, rad는 중심에서 거리*2, ring은 고리 단면.
        오브젝트 color: rgb=색조 곱, alpha=강도(키프레임으로 페이드)."""
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        nt = m.node_tree
        nt.nodes.clear()
        N = nt.nodes
        L = nt.links
        out = N.new('ShaderNodeOutputMaterial')
        tc = N.new('ShaderNodeTexCoord')
        sep = N.new('ShaderNodeSeparateXYZ')
        L.new(tc.outputs['Generated'], sep.inputs[0])

        def math(op, a=None, b=None, clamp=False):
            n = N.new('ShaderNodeMath')
            n.operation = op
            n.use_clamp = clamp
            if a is not None:
                if isinstance(a, (int, float)):
                    n.inputs[0].default_value = a
                else:
                    L.new(a, n.inputs[0])
            if b is not None:
                if isinstance(b, (int, float)):
                    n.inputs[1].default_value = b
                else:
                    L.new(b, n.inputs[1])
            return n.outputs[0]

        def ramp(fac, stops_):
            r = N.new('ShaderNodeValToRGB')
            els = r.color_ramp.elements
            # 기본 두 개를 첫·마지막 stop 으로 쓰고 나머지를 추가
            els[0].position, els[0].color = stops_[0][0], (*stops_[0][1], stops_[0][2])
            els[1].position, els[1].color = stops_[-1][0], (*stops_[-1][1], stops_[-1][2])
            for p, c, a in stops_[1:-1]:
                e = els.new(p)
                e.color = (*c, a)
            L.new(fac, r.inputs['Fac'])
            return r

        X, Z = sep.outputs['X'], sep.outputs['Z']
        if kind == 'spike':
            u = X
            v = math('POWER', math('SUBTRACT', 1.0, math('ABSOLUTE', math('SUBTRACT', math('MULTIPLY', Z, 2.0), 1.0))), power)
            r = ramp(u, stops)
            base = math('MULTIPLY', r.outputs['Alpha'], v)
        elif kind in ('rad', 'ring'):
            dx = math('SUBTRACT', X, 0.5)
            dz = math('SUBTRACT', Z, 0.5)
            cb = N.new('ShaderNodeCombineXYZ')
            L.new(dx, cb.inputs['X'])
            L.new(dz, cb.inputs['Z'])
            ln = N.new('ShaderNodeVectorMath')
            ln.operation = 'LENGTH'
            L.new(cb.outputs['Vector'], ln.inputs[0])
            d = math('MULTIPLY', ln.outputs['Value'], 2.0)
            r = ramp(d, stops)
            base = r.outputs['Alpha']
            if power != 1.0:
                base = math('POWER', base, power)
            if kind == 'ring' and noise:
                nz = N.new('ShaderNodeTexNoise')
                nz.inputs['Scale'].default_value = noise_scale
                nz.inputs['Detail'].default_value = 5.0
                L.new(tc.outputs['Generated'], nz.inputs['Vector'])
                # 0.5 근처로 가운데 맞춰 (1-noise) + noise*값
                mod = math('ADD', 1.0 - noise, math('MULTIPLY', nz.outputs['Fac'], noise * 2.0))
                base = math('MULTIPLY', base, mod)
        elif kind == 'star':
            ax = math('ABSOLUTE', math('SUBTRACT', math('MULTIPLY', X, 2.0), 1.0))
            az = math('ABSOLUTE', math('SUBTRACT', math('MULTIPLY', Z, 2.0), 1.0))
            ax1 = math('SUBTRACT', 1.0, ax, True)
            az1 = math('SUBTRACT', 1.0, az, True)
            harm = math('MULTIPLY', math('POWER', ax1, 3.0), math('POWER', az1, 14.0))
            varm = math('MULTIPLY', math('POWER', az1, 3.0), math('POWER', ax1, 14.0))
            # 대각 가는 빛(약하게)
            dg1 = math('POWER', math('SUBTRACT', 1.0, math('MULTIPLY', math('ABSOLUTE', math('SUBTRACT', ax, az)), 6.0), True), 4.0)
            dgf = math('MULTIPLY', dg1, math('MULTIPLY', math('POWER', ax1, 2.5), 0.4))
            core = math('POWER', math('MULTIPLY', ax1, az1), 6.0)
            fac = math('ADD', math('MAXIMUM', harm, varm), math('ADD', core, dgf))
            r = ramp(math('SUBTRACT', 1.0, math('MINIMUM', ax, az), True), stops)  # 색은 중심→바깥
            base = math('MULTIPLY', fac, r.outputs['Alpha'])
        oi = N.new('ShaderNodeObjectInfo')
        amp = math('MULTIPLY', math('MULTIPLY', base, strength), oi.outputs['Alpha'])
        col = N.new('ShaderNodeVectorMath')
        col.operation = 'MULTIPLY'
        L.new(r.outputs['Color'], col.inputs[0])
        L.new(oi.outputs['Color'], col.inputs[1])
        em = N.new('ShaderNodeEmission')
        L.new(col.outputs['Vector'], em.inputs['Color'])
        L.new(amp, em.inputs['Strength'])
        tr = N.new('ShaderNodeBsdfTransparent')
        add = N.new('ShaderNodeAddShader')
        L.new(tr.outputs['BSDF'], add.inputs[0])
        L.new(em.outputs['Emission'], add.inputs[1])
        L.new(add.outputs['Shader'], out.inputs['Surface'])
        try:
            m.surface_render_method = 'BLENDED'
        except Exception:
            pass
        return m

    def quad(name, mat, cname, centered=True, w=1.0, h=1.0):
        """XZ 평면(법선 -Y). centered=False면 x 0..w 로(스파이크용: 원점이 뿌리)."""
        me = bpy.data.meshes.new(name)
        x0, x1 = (-w / 2, w / 2) if centered else (0, w)
        vs = [(x0, 0, -h / 2), (x1, 0, -h / 2), (x1, 0, h / 2), (x0, 0, h / 2)]
        me.from_pydata(vs, [], [(0, 1, 2, 3)])
        me.update()
        o = bpy.data.objects.new(name, me)
        o.data.materials.append(mat)
        coll(cname).objects.link(o)
        o.color = (1, 1, 1, 1)
        o.visible_shadow = False
        return o

    def kf(o, path, frame, value, interp=None):
        setattr(o, path, value)
        o.keyframe_insert(path, frame=frame)

    def ease_out(t):
        return 1 - (1 - t) ** 3

    def key_curve(o, path, frames_values, interp='BEZIER'):
        """frames_values: [(frame, value)…]"""
        for f, v in frames_values:
            kf(o, path, f, v)
        ad = o.animation_data
        if ad and ad.action:
            # Blender 5: 레이어드 액션 — fcurve 접근은 channelbags
            try:
                for layer in ad.action.layers:
                    for strip in layer.strips:
                        for cb in strip.channelbags:
                            for fc in cb.fcurves:
                                for kp in fc.keyframe_points:
                                    kp.interpolation = interp
            except Exception:
                pass

    # ==================== 1) 섬광 폭발(스타버스트) ====================
    def build_starburst(seed=3):
        sc = reset()
        camera(persp=False)
        rnd = random.Random(seed)
        sc.frame_end = 12          # 0.4초
        gold = [(0, (1, 1, 1), 1), (0.25, (1, 0.93, 0.7), 0.95), (0.6, (1, 0.62, 0.15), 0.6), (1, (1, 0.25, 0.02), 0)]
        mspike = grad_mat('spike', 'spike', gold, strength=3.0, power=1.8)
        mspike_c = grad_mat('spike_c', 'spike', [(0, (1, 1, 1), 1), (0.4, (1, 0.97, 0.85), 0.9), (1, (1, 0.7, 0.3), 0)], strength=3.5, power=2.2)
        mhalo = grad_mat('halo', 'rad', [(0, (1, 0.97, 0.9), 1), (0.15, (1, 0.8, 0.4), 0.7), (0.5, (1, 0.45, 0.08), 0.25), (1, (0.6, 0.1, 0), 0)], strength=2.2, power=1.5)
        mcore = grad_mat('core', 'rad', [(0, (1, 1, 1), 1), (0.35, (1, 1, 0.95), 1), (0.6, (1, 0.9, 0.6), 0.5), (1, (1, 0.7, 0.2), 0)], strength=6.0, power=1.2)
        mring = grad_mat('flare_ring', 'ring', [(0, (1, 0.5, 0.1), 0), (0.78, (1, 0.5, 0.1), 0), (0.9, (1, 0.9, 0.6), 1), (0.96, (1, 0.6, 0.2), 0.5), (1, (1, 0.3, 0), 0)], strength=2.0, noise=0.5)
        mstar = grad_mat('star', 'star', [(0, (1, 1, 1), 1), (1, (1, 0.8, 0.4), 1)], strength=3.5)

        T = lambda f: (f - 1) / 11.0   # 0..1
        # 후광 — 크게 부풀었다 꺼진다
        halo = quad('halo', mhalo, 'fx')
        key_curve(halo, 'scale', [(1, (2, 1, 2)), (4, (13, 1, 13)), (12, (17, 1, 17))])
        key_curve(halo, 'color', [(1, (1, 1, 1, 0.2)), (3, (1, 1, 1, 1)), (8, (1, 1, 1, 0.5)), (12, (1, 1, 1, 0))])
        # 코어
        core = quad('core', mcore, 'fx')
        key_curve(core, 'scale', [(1, (0.5, 1, 0.5)), (3, (4.5, 1, 4.5)), (6, (3.2, 1, 3.2)), (12, (0.8, 1, 0.8))])
        key_curve(core, 'color', [(1, (1, 1, 1, 0.5)), (3, (1, 1, 1, 1)), (9, (1, 1, 1, 0.7)), (12, (1, 1, 1, 0))])
        # 광선: 길고 가는 것 + 짧고 굵은 것, 각도 불균일, 빠르게 뻗고 천천히 사그라듦
        n = 44
        for i in range(n):
            ang = (i / n) * 360.0 + rnd.uniform(-3, 3)
            ln = rnd.choice([rnd.uniform(5, 9.5), rnd.uniform(8, 11.5), rnd.uniform(3, 6)])
            wd = rnd.uniform(0.10, 0.42) * (1.5 if ln < 6 else 1.0)
            o = quad('spike%d' % i, mspike, 'fx', centered=False)
            o.rotation_euler = (0, math.radians(ang), 0)
            d = rnd.uniform(0, 1)    # 시작 지연 0~1프레임
            f0 = 1 + int(d * 1.5)
            key_curve(o, 'scale', [(f0, (0.05, 1, wd * 0.6)), (f0 + 2, (ln, 1, wd)), (12, (ln * 1.1, 1, wd * 0.5))])
            key_curve(o, 'color', [(f0, (1, 1, 1, 0.6)), (f0 + 2, (1, 1, 1, 1)), (8, (1, 1, 1, 0.45)), (12, (1, 1, 1, 0))])
        # 큰 십자 4줄(두껍고 빛나는) — 화면 끝까지
        for i, ang in enumerate((0, 90, 180, 270)):
            o = quad('major%d' % i, mspike_c, 'fx', centered=False)
            o.rotation_euler = (0, math.radians(ang), 0)
            L = 12.5 if ang in (0, 180) else 7.5
            key_curve(o, 'scale', [(1, (0.1, 1, 0.1)), (3, (L, 1, 0.55)), (12, (L * 1.08, 1, 0.12))])
            key_curve(o, 'color', [(1, (1, 1, 1, 0.5)), (3, (1, 1, 1, 1)), (8, (1, 1, 1, 0.55)), (12, (1, 1, 1, 0))])
        # 렌즈 링
        ring = quad('flare_ring', mring, 'fx')
        key_curve(ring, 'scale', [(1, (1.5, 1, 1.5)), (5, (9, 1, 9)), (12, (13, 1, 13))])
        key_curve(ring, 'color', [(1, (1, 1, 1, 0)), (2, (1, 1, 1, 1)), (8, (1, 1, 1, 0.4)), (12, (1, 1, 1, 0))])
        # 반짝이 별 몇 개(가운데 근처 + 바깥 흩어짐)
        for i in range(9):
            a = rnd.uniform(0, 6.283)
            rr = rnd.uniform(1.5, 6.5)
            o = quad('star%d' % i, mstar, 'fx')
            o.location = (math.cos(a) * rr, 0, math.sin(a) * rr)
            o.rotation_euler = (0, math.radians(rnd.uniform(0, 45)), 0)
            s = rnd.uniform(0.9, 2.2)
            f0 = rnd.randint(2, 6)
            key_curve(o, 'scale', [(f0, (0.1, 1, 0.1)), (f0 + 2, (s * 1.4, 1, s * 1.4)), (min(12, f0 + 5), (0.1, 1, 0.1))])
            key_curve(o, 'color', [(f0, (1, 1, 1, 0)), (f0 + 1, (1, 1, 1, 1)), (min(12, f0 + 5), (1, 1, 1, 0))])
        return 12

    # ==================== 2) 충격파 링 ====================
    def build_shockwave(seed=5):
        """일시적: 한 번 퍼지고 끝. 12프레임(0.4초), 마지막 1/3(9~12)에 빠르게 소멸, 잔상·반복 없음."""
        sc = reset()
        camera(persp=False)
        rnd = random.Random(seed)
        sc.frame_end = 12
        def ringmat(name, col):
            return grad_mat(name, 'ring', [(0, col, 0), (0.93, col, 0), (0.975, (1, 1, 1), 1), (0.992, col, 0.5), (1, col, 0)],
                            strength=1.5, noise=0.5, noise_scale=5.0)
        # 색수차: R·G·B 링 반지름이 조금씩 달라 가장자리에 무지개 번짐
        for ch, (col, k) in enumerate((((1, 0.1, 0.1), 1.05), ((0.1, 1, 0.2), 1.0), ((0.15, 0.3, 1), 0.95))):
            o = quad('ring_main%d' % ch, ringmat('ringc%d' % ch, col), 'fx')
            key_curve(o, 'scale', [(1, (1.5 * k, 1, 1.5 * k)), (6, (10 * k, 1, 10 * k)), (12, (21 * k, 1, 21 * k))])
            key_curve(o, 'color', [(1, (1, 1, 1, 0.3)), (2, (1, 1, 1, 1)), (8, (1, 1, 1, 0.85)), (11, (1, 1, 1, 0.15)), (12, (1, 1, 1, 0))])
        # 안쪽 빛: 처음에만 번쩍
        mh = grad_mat('inner', 'rad', [(0, (1, 0.95, 0.85), 0.6), (0.5, (1, 0.8, 0.5), 0.15), (1, (1, 0.5, 0.2), 0)], strength=0.8, power=2.0)
        o = quad('inner_glow', mh, 'fx')
        key_curve(o, 'scale', [(1, (3, 1, 3)), (3, (9, 1, 9)), (6, (14, 1, 14))])
        key_curve(o, 'color', [(1, (1, 1, 1, 0.8)), (2, (1, 1, 1, 1)), (4, (1, 1, 1, 0.2)), (6, (1, 1, 1, 0))])
        # 링 따라 튀는 불꽃 가루(링 반경과 같이 이동)
        ms = grad_mat('dust', 'spike', [(0, (1, 1, 1), 1), (0.5, (1, 0.85, 0.5), 0.8), (1, (1, 0.4, 0.1), 0)], strength=3.0, power=1.5)
        for i in range(80):
            ang = rnd.uniform(0, 360)
            r0 = rnd.uniform(0.82, 1.0)
            lg = rnd.uniform(0.4, 1.4)
            o = quad('dust%d' % i, ms, 'fx', centered=False)
            o.rotation_euler = (0, math.radians(ang), 0)
            for f, sv in ((1, 1.5), (6, 10), (12, 21)):
                rad = sv * 0.5 * r0 * 0.92
                o.location = (math.cos(math.radians(ang)) * rad, 0, -math.sin(math.radians(ang)) * rad)
                o.keyframe_insert('location', frame=f)
            key_curve(o, 'scale', [(1, (0.05, 1, 0.04)), (3, (lg, 1, 0.07)), (12, (lg * 1.3, 1, 0.03))])
            key_curve(o, 'color', [(1, (1, 1, 1, 0)), (2, (1, 1, 1, 1)), (8, (1, 1, 1, 0.7)), (11, (1, 1, 1, 0.1)), (12, (1, 1, 1, 0))])
        return 12

    # ==================== 5) 예고 글자 ====================
    TEXTS = {
        'chance': 'チャンス',
        'geki': '激熱',
        'kakuhen': '確変',
        'push': 'PUSH!!',
    }

    def text_obj(name, body, size, bevel, extrude, offset=0.0, cname='txt'):
        cu = bpy.data.curves.new(name, 'FONT')
        cu.body = body
        cu.font = bpy.data.fonts.load(FONT)
        cu.size = size
        cu.align_x = 'CENTER'
        cu.align_y = 'CENTER'
        cu.extrude = extrude
        cu.bevel_depth = bevel
        cu.bevel_resolution = 4
        cu.offset = offset
        o = bpy.data.objects.new(name, cu)
        coll(cname).objects.link(o)
        o.rotation_euler = (math.radians(90), 0, 0)
        return o

    def gold_red_mat(name, grad_stops, metal=1.0, rough=0.22, emit=0.0):
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        nt = m.node_tree
        nt.nodes.clear()
        out = nt.nodes.new('ShaderNodeOutputMaterial')
        b = nt.nodes.new('ShaderNodeBsdfPrincipled')
        tc = nt.nodes.new('ShaderNodeTexCoord')
        sep = nt.nodes.new('ShaderNodeSeparateXYZ')
        r = nt.nodes.new('ShaderNodeValToRGB')
        els = r.color_ramp.elements
        els[0].position, els[0].color = grad_stops[0][0], (*grad_stops[0][1], 1)
        els[1].position, els[1].color = grad_stops[-1][0], (*grad_stops[-1][1], 1)
        for p, c in grad_stops[1:-1]:
            e = els.new(p)
            e.color = (*c, 1)
        nt.links.new(tc.outputs['Generated'], sep.inputs[0])
        nt.links.new(sep.outputs['Y'], r.inputs['Fac'])
        nt.links.new(r.outputs['Color'], b.inputs['Base Color'])
        b.inputs['Metallic'].default_value = metal
        b.inputs['Roughness'].default_value = rough
        if emit:
            nt.links.new(r.outputs['Color'], b.inputs['Emission Color'])
            b.inputs['Emission Strength'].default_value = emit
        nt.links.new(b.outputs['BSDF'], out.inputs['Surface'])
        return m

    def flat_mat(name, color, metal=0.0, rough=0.4, emit=0.0):
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        nt = m.node_tree
        nt.nodes.clear()
        out = nt.nodes.new('ShaderNodeOutputMaterial')
        b = nt.nodes.new('ShaderNodeBsdfPrincipled')
        b.inputs['Base Color'].default_value = (*color, 1)
        b.inputs['Metallic'].default_value = metal
        b.inputs['Roughness'].default_value = rough
        if emit:
            b.inputs['Emission Color'].default_value = (*color, 1)
            b.inputs['Emission Strength'].default_value = emit
        nt.links.new(b.outputs['BSDF'], out.inputs['Surface'])
        return m

    def softbox(name, x, y, z, sw, sh, st, tilt=0.0):
        em = flat_mat(name + 'm', (1, 0.96, 0.9), emit=st)
        me = bpy.data.meshes.new(name)
        me.from_pydata([(-sw / 2, 0, -sh / 2), (sw / 2, 0, -sh / 2), (sw / 2, 0, sh / 2), (-sw / 2, 0, sh / 2)], [], [(0, 1, 2, 3)])
        o = bpy.data.objects.new(name, me)
        o.data.materials.append(em)
        o.location = (x, y, z)
        o.rotation_euler = (0, math.radians(tilt), 0)
        o.visible_camera = False
        coll('stage').objects.link(o)
        return o

    def build_text(key, seed=11):
        body = TEXTS[key]
        sc = reset(transparent=True)
        camera(persp=True)
        rnd = random.Random(seed)
        N = 36
        sc.frame_end = N
        # --- 글자 3겹: 본체(금→주황→빨강 그라데이션 금속) / 흰 테두리 / 진한 빨강 바깥 테두리 ---
        size = 4.2
        body_o = text_obj('face', body, size, bevel=0.07, extrude=0.16)
        white_o = text_obj('rim_white', body, size, bevel=0.06, extrude=0.05, offset=0.05)
        red_o = text_obj('rim_red', body, size, bevel=0.06, extrude=0.05, offset=0.19)
        bpy.context.view_layer.update()
        # 폭 맞춤: 4글자는 13.5BU, 짧은 건 더 작게
        w = body_o.dimensions.x
        k = min(10.5 / w, 1.5 if len(body) > 3 else 1.9)
        grad = [(0.0, (0.55, 0.02, 0.02)), (0.28, (0.95, 0.12, 0.05)), (0.5, (1.0, 0.55, 0.08)), (0.72, (1.0, 0.86, 0.35)), (1.0, (1.0, 0.97, 0.75))]
        body_o.data.materials.append(gold_red_mat('face', grad, metal=0.95, rough=0.2))
        white_o.data.materials.append(flat_mat('rim_white', (1, 0.98, 0.9), metal=0.2, rough=0.25, emit=0.4))
        red_o.data.materials.append(gold_red_mat('rim_red', [(0, (0.25, 0.0, 0.02)), (1, (0.6, 0.03, 0.05))], metal=0.7, rough=0.3))
        # 부모 빈 오브젝트(위치·회전·튀어나오기 애니메이션)
        pivot = bpy.data.objects.new('pivot', None)
        coll('txt').objects.link(pivot)
        for o, dz in ((body_o, -0.0), (white_o, 0.0), (red_o, 0.0)):
            o.parent = pivot
        # 뒤 겹 두 개는 살짝 뒤로(+Y)
        white_o.location = (0, 0.07, 0)
        red_o.location = (0, 0.13, 0)
        pivot.scale = (k, k, k)
        # 애니메이션: 0 → 뒤에서 튀어나옴(크게 1.35배 오버슈트) → 1.0 안착 → 은은한 호흡. Y축 회전(3D 입체), 약간 기울기.
        S = k
        key_curve(pivot, 'scale', [(1, (S * 0.05, S * 0.05, S * 0.05)), (5, (S * 1.4, S * 1.4, S * 1.4)), (8, (S * 0.94, S * 0.94, S * 0.94)),
                                  (11, (S * 1.06, S * 1.06, S * 1.06)), (14, (S, S, S)), (26, (S * 1.04, S * 1.04, S * 1.04)), (32, (S * 1.0, S * 1.0, S * 1.0)), (34, (S * 1.12, S * 1.12, S * 1.12)), (36, (S * 0.02, S * 0.02, S * 0.02))])
        key_curve(pivot, 'rotation_euler', [(1, (0, math.radians(-75), math.radians(8))), (5, (0, math.radians(18), math.radians(-4))),
                                           (9, (0, math.radians(-7), math.radians(2))), (13, (0, math.radians(2), 0)), (26, (0, math.radians(-3), math.radians(-1))), (32, (0, math.radians(3), math.radians(1))), (36, (0, math.radians(40), 0))])
        # --- 조명: 소프트박스 반사(카메라 뒤), 가운데로 훑는 하이라이트 ---
        softbox('sbT', 0, -16, 7, 24, 3.0, 2.2)
        softbox('sbB', 0, -16, -6, 24, 2.0, 1.6)
        softbox('sbL', -12, -16, 0, 3.0, 14, 1.6)
        softbox('sbR', 12, -16, 0, 3.0, 14, 1.6)
        sweep = softbox('sweep', -18, -15, 1.2, 1.6, 22, 7.0, tilt=18)
        key_curve(sweep, 'location', [(10, (-18, -15, 1.2)), (30, (18, -15, 1.2))], interp='LINEAR')
        for name, loc, e in (('key', (0, -14, 6), 900), ('fill', (-9, -10, -4), 300)):
            ld = bpy.data.lights.new(name, 'AREA')
            ld.energy = e
            ld.size = 12
            lo = bpy.data.objects.new(name, ld)
            lo.location = loc
            lo.rotation_euler = (math.radians(80 if loc[2] > 0 else 100), 0, 0)
            coll('stage').objects.link(lo)

        # --- 더하기 층(fx): 튀어나올 때 후광·광선, 쓸고 지나가며 반짝이 ---
        mhalo = grad_mat('thalo', 'rad', [(0, (1, 0.85, 0.5), 0.6), (0.4, (1, 0.5, 0.15), 0.2), (1, (0.9, 0.3, 0.05), 0)], strength=0.3, power=2.0)
        halo = quad('halo', mhalo, 'fx')
        halo.location = (0, 0.5, 0)
        key_curve(halo, 'scale', [(1, (4, 1, 4)), (5, (22, 1, 11)), (12, (20, 1, 9)), (36, (19, 1, 8.5))])
        key_curve(halo, 'color', [(1, (1, 1, 1, 0)), (4, (1, 1, 1, 1)), (14, (1, 1, 1, 0.55)), (30, (1, 1, 1, 0.4)), (35, (1, 1, 1, 0))])
        mspk = grad_mat('tspk', 'spike', [(0, (1, 1, 1), 1), (0.4, (1, 0.85, 0.45), 0.7), (1, (1, 0.35, 0.05), 0)], strength=2.8, power=1.8)
        for i in range(30):
            ang = rnd.uniform(0, 360)
            ln = rnd.uniform(4, 11)
            wd = rnd.uniform(0.08, 0.3)
            o = quad('tray%d' % i, mspk, 'fx', centered=False)
            o.location = (0, 0.4, 0)
            o.rotation_euler = (0, math.radians(ang), 0)
            f0 = rnd.randint(2, 4)
            key_curve(o, 'scale', [(f0, (0.05, 1, wd)), (f0 + 2, (ln, 1, wd)), (16, (ln * 1.1, 1, wd * 0.3))])
            key_curve(o, 'color', [(f0, (1, 1, 1, 0)), (f0 + 1, (1, 1, 1, 1)), (10, (1, 1, 1, 0.4)), (16, (1, 1, 1, 0))])
        mstar = grad_mat('tstar', 'star', [(0, (1, 1, 1), 1), (1, (1, 0.85, 0.5), 1)], strength=3.2)
        half = w * k / 2
        for i in range(14):
            o = quad('glint%d' % i, mstar, 'fx')
            gx = rnd.uniform(-half * 0.95, half * 0.95)
            gz = rnd.uniform(-1.5, 1.8)
            o.location = (gx, -0.6, gz)     # 글자 앞
            o.rotation_euler = (0, math.radians(rnd.uniform(0, 45)), 0)
            # 하이라이트가 훑는 시각(프레임 10~30)에 그 x 위치에서 반짝
            f0 = int(10 + (gx + 18) / 36 * 20) - 2
            s = rnd.uniform(1.0, 2.4)
            key_curve(o, 'scale', [(f0, (0.05, 1, 0.05)), (f0 + 2, (s, 1, s)), (f0 + 5, (0.05, 1, 0.05))])
            key_curve(o, 'color', [(f0, (1, 1, 1, 0)), (f0 + 1, (1, 1, 1, 1)), (f0 + 5, (1, 1, 1, 0))])
        # 첫 충격 큰 별
        o = quad('bigstar', mstar, 'fx')
        o.location = (-half * 0.8, -0.8, 1.2)
        key_curve(o, 'scale', [(4, (0.1, 1, 0.1)), (6, (6, 1, 6)), (14, (0.1, 1, 0.1))])
        key_curve(o, 'color', [(4, (1, 1, 1, 0)), (6, (1, 1, 1, 1)), (14, (1, 1, 1, 0))])
        return N

    # ---- 공용: 금속 반사용 무대(소프트박스·조명) ---------------------------------
    def stage_lights(k=1.0):
        bpy.context.scene.world['txtcolor'] = (0.22, 0.2, 0.17)
        softbox('sbT', 0, -16, 7, 24, 3.0, 2.2 * k)
        softbox('sbB', 0, -16, -6, 24, 2.0, 1.6 * k)
        softbox('sbL', -12, -16, 0, 3.0, 14, 1.6 * k)
        softbox('sbR', 12, -16, 0, 3.0, 14, 1.6 * k)
        for name, loc, e in (('key', (0, -14, 6), 900 * k), ('fill', (-9, -10, -4), 300 * k)):
            ld = bpy.data.lights.new(name, 'AREA')
            ld.energy = e
            ld.size = 12
            lo = bpy.data.objects.new(name, ld)
            lo.location = loc
            lo.rotation_euler = (math.radians(80 if loc[2] > 0 else 100), 0, 0)
            coll('stage').objects.link(lo)

    def star_mesh(name, r_out, r_in, depth, points=5):
        import bmesh
        bm = bmesh.new()
        pts = []
        for i in range(points * 2):
            a = math.pi / points * i + math.pi / 2
            r = r_out if i % 2 == 0 else r_in
            pts.append((math.cos(a) * r, math.sin(a) * r))
        vs = [bm.verts.new((x, y, 0)) for x, y in pts]
        f = bm.faces.new(vs)
        res = bmesh.ops.extrude_face_region(bm, geom=[f])
        vv = [e for e in res['geom'] if isinstance(e, bmesh.types.BMVert)]
        bmesh.ops.translate(bm, verts=vv, vec=(0, 0, depth))
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)
        bm.free()
        return me

    GOLD_STOPS = [(0.0, (0.55, 0.28, 0.02)), (0.35, (0.95, 0.6, 0.1)), (0.6, (1.0, 0.85, 0.35)), (1.0, (1.0, 0.96, 0.7))]

    # ==================== 3) 금빛 파티클·코인 폭발 ====================
    def build_coins(seed=21):
        sc = reset(transparent=True)
        camera(persp=True)
        rnd = random.Random(seed)
        N = 24                    # 0.8초
        sc.frame_end = N
        stage_lights(1.1)
        mgold = gold_red_mat('gold', GOLD_STOPS, metal=1.0, rough=0.25, emit=0.22)
        mrim = flat_mat('rim', (0.7, 0.4, 0.05), metal=1.0, rough=0.3)
        # 코인 원본: 원기둥 + 안쪽 홈 느낌의 얇은 링
        import bmesh
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, segments=32, radius1=0.6, radius2=0.6, depth=0.09)
        bmesh.ops.create_cone(bm, cap_ends=True, segments=32, radius1=0.44, radius2=0.44, depth=0.15)
        coin_me = bpy.data.meshes.new('coin')
        bm.to_mesh(coin_me); bm.free()
        coin_me.materials.append(mgold)
        for p in coin_me.polygons:
            p.use_smooth = True
        star_me = star_mesh('starm', 0.5, 0.22, 0.12)
        star_me.materials.append(mgold)
        mstar = grad_mat('cstar', 'star', [(0, (1, 1, 1), 1), (1, (1, 0.85, 0.45), 1)], strength=3.2)
        mdot = grad_mat('cdot', 'rad', [(0, (1, 0.95, 0.7), 1), (0.4, (1, 0.7, 0.2), 0.5), (1, (1, 0.4, 0), 0)], strength=2.0, power=1.5)
        # 중앙 섬광(짧게)
        fl = quad('flash', mdot, 'fx')
        key_curve(fl, 'scale', [(1, (3, 1, 3)), (3, (10, 1, 10)), (7, (4, 1, 4))])
        key_curve(fl, 'color', [(1, (1, 1, 1, 1)), (3, (1, 1, 1, 0.8)), (7, (1, 1, 1, 0))])
        g = -42.0
        for i in range(56):
            isstar = (i % 4 == 3)
            o = bpy.data.objects.new('p%d' % i, star_me if isstar else coin_me)
            coll('txt').objects.link(o)
            ang = rnd.uniform(-70, 70)                    # 위로 부채꼴
            sp = rnd.uniform(14, 30)
            vx = math.sin(math.radians(ang)) * sp * 0.9
            vz = math.cos(math.radians(ang)) * sp
            x0, z0 = rnd.uniform(-0.6, 0.6), rnd.uniform(-1.2, -0.2)
            sz = rnd.uniform(0.7, 1.5) * (1.1 if isstar else 1.0)
            spin = [rnd.uniform(-900, 900) for _ in range(3)]
            r0 = [rnd.uniform(0, 360) for _ in range(3)]
            life_end = 24
            for f in range(1, life_end + 1):
                t = (f - 1) / FPS
                o.location = (x0 + vx * t, rnd.uniform(-0.01, 0.01) + (-0.3 if isstar else 0.0), z0 + vz * t + 0.5 * g * t * t)
                o.rotation_euler = [math.radians(r0[j] + spin[j] * t) for j in range(3)]
                grow = min(1.0, (f - 1) / 3.0)
                shrink = max(0.0, min(1.0, (life_end - f + 1) / 3.0))
                s = sz * grow * shrink
                o.scale = (s, s, s)
                o.keyframe_insert('location', frame=f)
                o.keyframe_insert('rotation_euler', frame=f)
                o.keyframe_insert('scale', frame=f)
            # 반짝임(더하기): 코인이 비스듬한 순간
            tg = rnd.randint(4, 15)
            gl = quad('g%d' % i, mstar, 'fx')
            for f in range(1, life_end + 1):
                t = (f - 1) / FPS
                gl.location = (x0 + vx * t, -0.7, z0 + vz * t + 0.5 * g * t * t)
                gl.keyframe_insert('location', frame=f)
            gs = rnd.uniform(0.8, 1.8)
            key_curve(gl, 'scale', [(max(1, tg - 1), (0.05, 1, 0.05)), (tg + 1, (gs, 1, gs)), (tg + 4, (0.05, 1, 0.05))])
            key_curve(gl, 'color', [(max(1, tg - 1), (1, 1, 1, 0)), (tg, (1, 1, 1, 1)), (tg + 4, (1, 1, 1, 0))])
        return N

    # ==================== 4) 집중선/속도선(루프) ====================
    def build_speedlines(seed=9, tint=(1, 1, 1), edge=(0.85, 0.92, 1.0)):
        sc = reset()
        camera(persp=False)
        rnd = random.Random(seed)
        T = 12                     # 0.4초 한 바퀴, 12프레임 루프
        sc.frame_end = T
        ms = grad_mat('sl', 'spike', [(0, tint, 0.0), (0.35, tint, 0.9), (0.7, edge, 1.0), (1, edge, 0)], strength=2.2, power=1.2)
        n = 110
        for i in range(n):
            ang = rnd.uniform(0, 360)
            ln = rnd.uniform(2.0, 6.0) if rnd.random() < 0.8 else rnd.uniform(5, 9)
            wd = rnd.uniform(0.05, 0.16) if rnd.random() < 0.85 else rnd.uniform(0.2, 0.34)
            ph = rnd.uniform(0, 1)
            spd = 1 if rnd.random() < 0.75 else 2        # 일부는 한 바퀴에 두 번(여전히 루프 정합)
            o = quad('sl%d' % i, ms, 'fx')
            o.rotation_euler = (0, math.radians(ang), 0)
            for f in range(1, T + 2):
                u = (((f - 1) / T) * spd + ph) % 1.0
                rad = 13.0 - u * 10.0                        # 바깥(13)→안쪽(3)
                a = math.radians(ang)
                o.location = (math.cos(a) * rad, 0, -math.sin(a) * rad)
                o.scale = (ln, 1, wd)
                fade = min(1.0, u / 0.12) * min(1.0, (1.0 - u) / 0.25)   # 가장자리에서 켜지고 안쪽에서 꺼짐
                o.color = (1, 1, 1, max(0.0, fade))
                o.keyframe_insert('location', frame=f)
                o.keyframe_insert('scale', frame=f)
                o.keyframe_insert('color', frame=f)
            if spd == 2 and False:
                pass
        # 선형 보간(루프 부드럽게)
        for o in bpy.data.objects:
            if o.animation_data and o.animation_data.action:
                try:
                    for layer in o.animation_data.action.layers:
                        for strip in layer.strips:
                            for cb in strip.channelbags:
                                for fc in cb.fcurves:
                                    for kp in fc.keyframe_points:
                                        kp.interpolation = 'LINEAR'
                except Exception:
                    pass
        # 가장자리 비네트(어두운 푸른 번짐 아님: 더하기만 — 가장자리 은은한 빛)
        mv = grad_mat('vig', 'rad', [(0, tint, 0), (0.7, tint, 0.0), (1.0, edge, 0.4)], strength=0.25)
        return T

    # ==================== 6) 테마 ====================
    def fire_mat(name, palette, scale=2.2, seed=0.0, thr=1.0, strength=2.6):
        """불길: 거리 마스크 + 4D 노이즈(W 애니메이션) → 색 램프. palette 'orange' | 'cyan'."""
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        nt = m.node_tree
        nt.nodes.clear()
        N = nt.nodes
        L = nt.links
        out = N.new('ShaderNodeOutputMaterial')
        tc = N.new('ShaderNodeTexCoord')
        sep = N.new('ShaderNodeSeparateXYZ')
        L.new(tc.outputs['Generated'], sep.inputs[0])

        def mth(op, a=None, b=None, clamp=False):
            n = N.new('ShaderNodeMath')
            n.operation = op
            n.use_clamp = clamp
            for idx, v in ((0, a), (1, b)):
                if v is None:
                    continue
                if isinstance(v, (int, float)):
                    n.inputs[idx].default_value = v
                else:
                    L.new(v, n.inputs[idx])
            return n.outputs[0]
        cb = N.new('ShaderNodeCombineXYZ')
        L.new(mth('SUBTRACT', sep.outputs['X'], 0.5), cb.inputs['X'])
        L.new(mth('SUBTRACT', sep.outputs['Z'], 0.5), cb.inputs['Z'])
        ln = N.new('ShaderNodeVectorMath')
        ln.operation = 'LENGTH'
        L.new(cb.outputs['Vector'], ln.inputs[0])
        d = mth('MULTIPLY', ln.outputs['Value'], 2.0)
        nz = N.new('ShaderNodeTexNoise')
        try:
            nz.noise_dimensions = '4D'
        except Exception:
            pass
        nz.inputs['Scale'].default_value = scale
        nz.inputs['Detail'].default_value = 6.0
        nz.inputs['Roughness'].default_value = 0.6
        L.new(tc.outputs['Generated'], nz.inputs['Vector'])
        wnode = nz.inputs['W'] if 'W' in nz.inputs else None
        dens = mth('SUBTRACT', mth('SUBTRACT', thr, d), mth('MULTIPLY', mth('SUBTRACT', nz.outputs['Fac'], 0.5), -1.2))
        dens = mth('MAXIMUM', dens, 0.0)
        dens = mth('MINIMUM', mth('MULTIPLY', dens, 1.2), 0.66)
        r = N.new('ShaderNodeValToRGB')
        if palette == 'orange':
            stops = [(0.0, (0.2, 0.0, 0.0), 0.0), (0.18, (0.7, 0.05, 0.0), 0.35), (0.4, (1.0, 0.3, 0.02), 0.8), (0.7, (1.0, 0.7, 0.15), 1.0), (1.0, (1.0, 1.0, 0.85), 1.0)]
        else:   # cyan(하늘색 불꽃)
            stops = [(0.0, (0.0, 0.05, 0.25), 0.0), (0.18, (0.0, 0.25, 0.8), 0.35), (0.4, (0.1, 0.6, 1.0), 0.8), (0.7, (0.5, 0.9, 1.0), 1.0), (1.0, (0.95, 1.0, 1.0), 1.0)]
        els = r.color_ramp.elements
        els[0].position, els[0].color = stops[0][0], (*stops[0][1], stops[0][2])
        els[1].position, els[1].color = stops[-1][0], (*stops[-1][1], stops[-1][2])
        for p, c, a in stops[1:-1]:
            e = els.new(p)
            e.color = (*c, a)
        L.new(dens, r.inputs['Fac'])
        oi = N.new('ShaderNodeObjectInfo')
        amp = mth('MULTIPLY', mth('MULTIPLY', r.outputs['Alpha'], strength), oi.outputs['Alpha'])
        em = N.new('ShaderNodeEmission')
        L.new(r.outputs['Color'], em.inputs['Color'])
        L.new(amp, em.inputs['Strength'])
        tr = N.new('ShaderNodeBsdfTransparent')
        add = N.new('ShaderNodeAddShader')
        L.new(tr.outputs['BSDF'], add.inputs[0])
        L.new(em.outputs['Emission'], add.inputs[1])
        L.new(add.outputs['Shader'], out.inputs['Surface'])
        try:
            m.surface_render_method = 'BLENDED'
        except Exception:
            pass
        # W 애니메이션(불길이 일렁이게)
        if wnode is not None:
            for f in range(1, 31):
                wnode.default_value = seed + f * 0.35
                wnode.keyframe_insert('default_value', frame=f)
        return m

    def build_fire(seed=31):
        """불꽃 소방대 풍: 주황 화염 폭발 + 하늘색 불꽃 + 불티. 0.6초(18프레임)."""
        sc = reset()
        camera(persp=False)
        rnd = random.Random(seed)
        N = 18
        sc.frame_end = N
        mo = fire_mat('fireO', 'orange', scale=2.4, seed=0.0, thr=0.8, strength=0.65)
        mc = fire_mat('fireC', 'cyan', scale=2.8, seed=7.0, thr=0.75, strength=0.65)
        # 가운데 큰 화구 + 옆 불덩이들
        def puff(name, m, cx, cz, s0, s1, fa, fb, fc):
            o = quad(name, m, 'fx')
            o.location = (cx, 0, cz)
            key_curve(o, 'scale', [(fa, (s0, 1, s0)), (fb, (s1, 1, s1 * 1.1)), (fc, (s1 * 1.35, 1, s1 * 1.6))])
            key_curve(o, 'color', [(fa, (1, 1, 1, 0.0)), (fa + 1, (1, 1, 1, 1)), (fb, (1, 1, 1, 0.9)), (fc, (1, 1, 1, 0))])
            return o
        puff('core', mo, 0, 0, 3, 9, 1, 7, 17)
        for i in range(5):
            a = rnd.uniform(0, 6.283)
            r = rnd.uniform(2.5, 6.5)
            sp = rnd.uniform(5, 9)
            f0 = rnd.randint(1, 4)
            puff('o%d' % i, mo, math.cos(a) * r, math.sin(a) * r * 0.7, 1.5, sp, f0, f0 + 5, min(N, f0 + 12))
        for i in range(5):
            a = rnd.uniform(0, 6.283)
            r = rnd.uniform(1.5, 5.0)
            sp = rnd.uniform(4, 7)
            f0 = rnd.randint(2, 5)
            puff('c%d' % i, mc, math.cos(a) * r, math.sin(a) * r * 0.7, 1.0, sp, f0, f0 + 4, min(N, f0 + 10))
        # 불티: 위로 튀는 가는 불똥(속도 방향으로 늘어난 스파이크) 주황·하늘
        msp = grad_mat('ember', 'spike', [(0, (1, 0.95, 0.7), 0.0), (0.4, (1, 0.7, 0.2), 1.0), (1, (1, 0.2, 0), 1)], strength=3.2, power=1.0)
        msc = grad_mat('emberc', 'spike', [(0, (0.8, 1, 1), 0.0), (0.4, (0.3, 0.8, 1), 1.0), (1, (0.1, 0.3, 1), 1)], strength=3.2, power=1.0)
        g = -18.0
        for i in range(90):
            o = quad('em%d' % i, msc if i % 4 == 0 else msp, 'fx')
            ang = rnd.uniform(0, 360)
            sp = rnd.uniform(6, 22)
            vx, vz = math.cos(math.radians(ang)) * sp, math.sin(math.radians(ang)) * sp * 0.85 + 4
            x0, z0 = rnd.uniform(-1, 1), rnd.uniform(-0.6, 0.6)
            f0 = rnd.randint(1, 4)
            life = rnd.randint(7, 13)
            for f in range(f0, min(N, f0 + life) + 1):
                t = (f - f0) / FPS
                x, z = x0 + vx * t, z0 + vz * t + 0.5 * g * t * t
                vzt = vz + g * t
                heading = math.atan2(-vzt, vx)       # XZ 평면에서 속도 방향(Y축 회전은 부호 반대)
                o.location = (x, 0, z)
                o.rotation_euler = (0, math.atan2(vzt, vx) * -1, 0)
                ln = max(0.25, min(1.4, math.hypot(vx, vzt) * 0.05))
                o.scale = (ln, 1, 0.07 + 0.04 * (i % 3))
                u = (f - f0) / max(1, life)
                o.color = (1, 1, 1, max(0.0, (1 - u) ** 1.2 * min(1.0, (f - f0 + 1) / 1.5)))
                for pth in ('location', 'rotation_euler', 'scale', 'color'):
                    o.keyframe_insert(pth, frame=f)
        return N

    def slime_ramp_mat(name):
        return flat_mat(name, (0.1, 0.55, 1.0), metal=0.0, rough=0.06, emit=0.5)

    def build_slime(seed=41):
        """전생슬 풍: 파랑·초록 슬라임 덩어리가 터져 물방울이 튄다. 0.6초. 윤기 나는 3D 방울(투명 배경) + 더하기 물빛."""
        sc = reset(transparent=True)
        camera(persp=True)
        rnd = random.Random(seed)
        N = 18
        sc.frame_end = N
        stage_lights(0.9)
        def gel(name, col):
            m = bpy.data.materials.new(name)
            m.use_nodes = True
            nt = m.node_tree
            nt.nodes.clear()
            out = nt.nodes.new('ShaderNodeOutputMaterial')
            b = nt.nodes.new('ShaderNodeBsdfPrincipled')
            b.inputs['Base Color'].default_value = (*col, 1)
            b.inputs['Roughness'].default_value = 0.05
            b.inputs['Metallic'].default_value = 0.0
            for k, v in (('Coat Weight', 1.0), ('Coat Roughness', 0.02), ('Specular IOR Level', 1.0), ('Subsurface Weight', 0.5)):
                if k in b.inputs:
                    b.inputs[k].default_value = v
            for k in ('Emission Color',):
                if k in b.inputs:
                    b.inputs[k].default_value = (*col, 1)
            if 'Emission Strength' in b.inputs:
                b.inputs['Emission Strength'].default_value = 0.12
            nt.links.new(b.outputs['BSDF'], out.inputs['Surface'])
            return m
        mb = gel('gelB', (0.05, 0.45, 1.0))
        mg = gel('gelG', (0.1, 0.9, 0.45))
        import bmesh
        def sphere_me(name, mat):
            bm = bmesh.new()
            bmesh.ops.create_uvsphere(bm, u_segments=32, v_segments=16, radius=0.5)
            me = bpy.data.meshes.new(name)
            bm.to_mesh(me); bm.free()
            for p in me.polygons:
                p.use_smooth = True
            me.materials.append(mat)
            return me
        meB, meG = sphere_me('dropB', mb), sphere_me('dropG', mg)
        # 가운데 큰 덩어리: 퍼지며 일렁였다가 터져 사라짐(스쿼시)
        big = bpy.data.objects.new('blob', meB)
        coll('txt').objects.link(big)
        key_curve(big, 'scale', [(1, (0.2, 0.2, 0.2)), (3, (5.5, 4.2, 4.8)), (5, (4.6, 5.2, 4.8)), (7, (6.2, 3.4, 5.0)), (9, (3.0, 3.0, 3.0)), (10, (0.05, 0.05, 0.05))])
        # 튀는 물방울(타원: 속도 방향으로 늘어남)
        g = -38.0
        for i in range(70):
            o = bpy.data.objects.new('d%d' % i, meG if i % 3 == 0 else meB)
            coll('txt').objects.link(o)
            ang = rnd.uniform(0, 360)
            sp = rnd.uniform(7, 26)
            vx, vz = math.cos(math.radians(ang)) * sp, math.sin(math.radians(ang)) * sp * 0.9 + 4
            r = rnd.uniform(0.18, 0.7)
            f0 = rnd.randint(8, 10)
            x0 = z0 = 0.0
            life = rnd.randint(6, 9)
            for f in range(f0, min(N, f0 + life) + 1):
                t = (f - f0) / FPS
                x, z = x0 + vx * t, z0 + vz * t + 0.5 * g * t * t
                vzt = vz + g * t
                o.location = (x, rnd.uniform(-0.01, 0.01) + (rnd.random() * -0.4), z)
                stretch = 1.0 + min(2.2, math.hypot(vx, vzt) * 0.07)
                o.rotation_euler = (0, -math.atan2(vzt, vx), 0)
                u = (f - f0) / max(1, life)
                s = r * (1 - u ** 3) * min(1.0, (f - f0 + 1) / 1.5)
                o.scale = (s * stretch, s * 0.8, s * 0.8)
                for pth in ('location', 'rotation_euler', 'scale'):
                    o.keyframe_insert(pth, frame=f)
        # 더하기: 물빛 파문 링 + 흰 하이라이트 반짝(방울 터질 때)
        mring = grad_mat('wring', 'ring', [(0, (0.4, 1, 0.9), 0), (0.93, (0.4, 1, 0.9), 0), (0.975, (0.8, 1, 1), 1), (1, (0.2, 0.7, 1), 0)], strength=1.2, noise=0.4)
        ring = quad('wring', mring, 'fx')
        key_curve(ring, 'scale', [(8, (2, 1, 2)), (12, (9, 1, 9)), (18, (14, 1, 14))])
        key_curve(ring, 'color', [(8, (1, 1, 1, 0)), (9, (1, 1, 1, 1)), (13, (1, 1, 1, 0.4)), (16, (1, 1, 1, 0))])
        mglow = grad_mat('wglow', 'rad', [(0, (0.7, 1, 1), 0.8), (0.4, (0.1, 0.8, 1), 0.35), (1, (0, 0.2, 0.8), 0)], strength=0.55, power=1.6)
        gl = quad('wglow', mglow, 'fx')
        key_curve(gl, 'scale', [(1, (2, 1, 2)), (4, (10, 1, 8)), (9, (14, 1, 12)), (13, (10, 1, 8))])
        key_curve(gl, 'color', [(1, (1, 1, 1, 0)), (3, (1, 1, 1, 1)), (9, (1, 1, 1, 0.8)), (13, (1, 1, 1, 0))])
        mst = grad_mat('wstar', 'star', [(0, (1, 1, 1), 1), (1, (0.6, 1, 1), 1)], strength=3.0)
        for i in range(10):
            a = rnd.uniform(0, 6.283)
            rr = rnd.uniform(2, 6)
            o = quad('wst%d' % i, mst, 'fx')
            o.location = (math.cos(a) * rr, -0.8, math.sin(a) * rr * 0.8)
            f0 = rnd.randint(9, 13)
            s = rnd.uniform(0.8, 1.6)
            key_curve(o, 'scale', [(f0, (0.05, 1, 0.05)), (f0 + 1, (s, 1, s)), (f0 + 4, (0.05, 1, 0.05))])
            key_curve(o, 'color', [(f0, (1, 1, 1, 0)), (f0 + 1, (1, 1, 1, 1)), (f0 + 4, (1, 1, 1, 0))])
        return N

    # ==================== 7) 무지개 프리미엄 섬광 ====================
    def hsv(h, s=1.0, v=1.0):
        import colorsys
        return colorsys.hsv_to_rgb(h % 1.0, s, v)

    def build_rainbow(seed=51):
        sc = reset()
        camera(persp=False)
        rnd = random.Random(seed)
        N = 18      # 0.6초
        sc.frame_end = N
        mwh = grad_mat('rb_spike', 'spike', [(0, (1, 1, 1), 1), (0.35, (1, 1, 1), 0.9), (1, (1, 1, 1), 0)], strength=3.0, power=1.6)
        mcore = grad_mat('rb_core', 'rad', [(0, (1, 1, 1), 1), (0.3, (1, 1, 1), 0.9), (0.6, (1, 1, 1), 0.3), (1, (1, 1, 1), 0)], strength=1.4, power=1.4)
        mhalo = grad_mat('rb_halo', 'rad', [(0, (1, 1, 1), 0.5), (0.5, (1, 1, 1), 0.2), (1, (1, 1, 1), 0)], strength=0.3, power=1.6)
        mring = grad_mat('rb_ring', 'ring', [(0, (1, 1, 1), 0), (0.84, (1, 1, 1), 0), (0.95, (1, 1, 1), 1), (1, (1, 1, 1), 0)], strength=1.1, noise=0.35)
        mstar = grad_mat('rb_star', 'star', [(0, (1, 1, 1), 1), (1, (1, 1, 1), 1)], strength=3.4)
        # 무지개 후광: 색 다른 큰 원을 겹쳐 가운데는 흰색, 가장자리는 무지개
        for i in range(6):
            o = quad('hh%d' % i, mhalo, 'fx')
            o.color = (*hsv(i / 6.0, 0.9, 1.0), 1.0)
            k = 1.0 + i * 0.12
            key_curve(o, 'scale', [(1, (3 * k, 1, 3 * k)), (5, (14 * k, 1, 14 * k)), (18, (20 * k, 1, 20 * k))])
            key_curve(o, 'color', [(1, (*hsv(i / 6.0, 0.9, 1.0), 0.0)), (3, (*hsv(i / 6.0, 0.9, 1.0), 1)), (9, (*hsv(i / 6.0, 0.9, 1.0), 0.55)), (18, (*hsv(i / 6.0, 0.9, 1.0), 0))])
        # 광선: 각도마다 색상
        n = 72
        for i in range(n):
            ang = (i / n) * 360 + rnd.uniform(-2, 2)
            col = hsv(i / n * 2.0, 0.85, 1.0)
            ln = rnd.choice([rnd.uniform(6, 10), rnd.uniform(9, 13), rnd.uniform(4, 7)])
            wd = rnd.uniform(0.12, 0.45)
            f0 = 1 + int(rnd.uniform(0, 1.6))
            o = quad('rs%d' % i, mwh, 'fx', centered=False)
            o.rotation_euler = (0, math.radians(ang), 0)
            key_curve(o, 'scale', [(f0, (0.05, 1, wd * 0.6)), (f0 + 2, (ln, 1, wd)), (18, (ln * 1.15, 1, wd * 0.3))])
            key_curve(o, 'color', [(f0, (*col, 0.5)), (f0 + 2, (*col, 1)), (11, (*col, 0.55)), (18, (*col, 0))])
        # 코어
        o = quad('rb_core', mcore, 'fx')
        key_curve(o, 'scale', [(1, (0.5, 1, 0.5)), (3, (2.6, 1, 2.6)), (8, (2.2, 1, 2.2)), (18, (0.6, 1, 0.6))])
        key_curve(o, 'color', [(1, (1, 1, 1, 0.5)), (3, (1, 1, 1, 1)), (13, (1, 1, 1, 0.6)), (18, (1, 1, 1, 0))])
        # 프리즘 링 6겹(반지름 어긋남) — 한 번 퍼지고 끝
        for i in range(6):
            o = quad('rr%d' % i, mring, 'fx')
            col = hsv(i / 6.0, 0.9, 1.0)
            k = 1.0 + (i - 2.5) * 0.045
            key_curve(o, 'scale', [(2, (1.5 * k, 1, 1.5 * k)), (7, (13 * k, 1, 13 * k)), (16, (26 * k, 1, 26 * k))])
            key_curve(o, 'color', [(2, (*col, 0)), (3, (*col, 1)), (12, (*col, 0.8)), (16, (*col, 0))])
        # 반짝이 별(다색)
        for i in range(16):
            a = rnd.uniform(0, 6.283)
            rr = rnd.uniform(1.5, 8)
            o = quad('rst%d' % i, mstar, 'fx')
            o.location = (math.cos(a) * rr, 0, math.sin(a) * rr * 0.8)
            o.rotation_euler = (0, math.radians(rnd.uniform(0, 45)), 0)
            col = hsv(rnd.random(), 0.45, 1.0)
            f0 = rnd.randint(3, 11)
            s = rnd.uniform(1.0, 2.6)
            key_curve(o, 'scale', [(f0, (0.05, 1, 0.05)), (f0 + 2, (s, 1, s)), (f0 + 5, (0.05, 1, 0.05))])
            key_curve(o, 'color', [(f0, (*col, 0)), (f0 + 1, (*col, 1)), (f0 + 5, (*col, 0))])
        # 큰 십자
        for i, ang in enumerate((0, 90, 180, 270)):
            o = quad('rbmaj%d' % i, mwh, 'fx', centered=False)
            o.rotation_euler = (0, math.radians(ang), 0)
            L_ = 13 if ang in (0, 180) else 8
            key_curve(o, 'scale', [(1, (0.1, 1, 0.1)), (3, (L_, 1, 0.6)), (18, (L_ * 1.1, 1, 0.1))])
            key_curve(o, 'color', [(1, (1, 1, 1, 0.5)), (3, (1, 1, 1, 1)), (11, (1, 1, 1, 0.5)), (18, (1, 1, 1, 0))])
        return N

    # ---- 렌더 ---------------------------------------------------------------------
    EFFECTS = {
        'starburst': (build_starburst, False),
        'shockwave': (build_shockwave, False),
        'coins': (build_coins, True),
        'speedlines': (build_speedlines, False),
        'fire': (build_fire, False),
        'slime': (build_slime, True),
        'rainbow': (build_rainbow, False),
    }
    for _k in TEXTS:
        EFFECTS['text_' + _k] = ((lambda k=_k: build_text(k)), True)

    def set_visible(names):
        for c in bpy.data.collections:
            c.hide_render = c.name not in names
            c.hide_viewport = c.hide_render

    def render_range(effect, first=None, last=None, out_dir=None):
        """effect 를 만들고 프레임 first..last 를 raw 로 렌더. 글자는 txt(투명) · fx(더하기) 두 번."""
        build, has_text = EFFECTS[effect]
        n = build()
        sc = bpy.context.scene
        first = first or 1
        last = last or n
        base = out_dir or os.path.join(OUT, effect, 'raw')
        os.makedirs(base, exist_ok=True)
        passes = [('add', ['fx'], False)]
        if has_text:
            passes = [('txt', ['txt', 'stage'], True), ('add', ['fx'], False)]
        for tag, cols, transp in passes:
            set_visible(cols)
            wc = tuple(bpy.context.scene.world.get('txtcolor', (0, 0, 0))) if transp else (0, 0, 0)
            bpy.context.scene.world.node_tree.nodes['Background'].inputs['Color'].default_value = (*wc, 1)
            sc.render.film_transparent = transp
            sc.render.image_settings.color_mode = 'RGBA' if transp else 'RGB'
            for f in range(first, last + 1):
                sc.frame_set(f)
                sc.render.filepath = os.path.join(base, '%s_%04d.png' % (tag, f))
                bpy.ops.render.render(write_still=True)
        meta = {'effect': effect, 'frames': n, 'fps': FPS, 'size': [W, H], 'passes': [p[0] for p in passes]}
        json.dump(meta, open(os.path.join(OUT, effect, 'meta.json'), 'w'))
        return n

    def preview(effect, frame, path=None):
        """사장님 창에서 한 프레임 시험(add 층만, 글자는 txt+add 둘 다 raw 에)."""
        return render_range(effect, frame, frame)


# =============================================================================
#  후처리 (시스템 파이썬: numpy + PIL)
# =============================================================================
def _post_imports():
    global np, Image, ImageFilter
    import numpy as np
    from PIL import Image, ImageFilter
    return np, Image, ImageFilter


def blur_rgb(arr, radius):
    """arr float32 HxWx3. 1/4 로 평균 풀링 → FFT 가우시안(float, 8비트 양자화 없음) → 양선형 되돌림."""
    np, Image, _ = _post_imports()
    s = 4
    h, w = arr.shape[:2]
    sm = arr[:h // s * s, :w // s * s].reshape(h // s, s, w // s, s, 3).mean(axis=(1, 3))
    sig = max(radius / s, 0.5)
    hh, ww = sm.shape[:2]
    fy = np.fft.fftfreq(hh)[:, None]
    fx = np.fft.rfftfreq(ww)[None, :]
    g = np.exp(-2 * (np.pi * sig) ** 2 * (fy ** 2 + fx ** 2)).astype('float32')
    out = np.empty_like(sm)
    for c in range(3):
        out[..., c] = np.fft.irfft2(np.fft.rfft2(sm[..., c]) * g, s=sm.shape[:2])
    up = np.empty((h, w, 3), dtype='float32')
    for c in range(3):
        up[..., c] = np.asarray(Image.fromarray(np.ascontiguousarray(out[..., c])).resize((w, h), Image.BILINEAR))
    return np.clip(up, 0, None)


def dither8(x):
    """float 0..1 → uint8, 살짝 노이즈를 섞어 띠(밴딩)를 없앤다."""
    np, _, _ = _post_imports()
    n = (np.random.default_rng(1).random(x.shape).astype('float32') - 0.5)
    return np.clip(x * 255 + n + 0.5, 0, 255).astype('uint8')


def bloom(rgb, gain=1.0):
    """다중 스케일 빛 번짐. 밝은 곳(0.55 이상)만 뽑아서 번지게 한다."""
    np, _, _ = _post_imports()
    hi = np.clip((rgb - 0.45) / 0.55, 0, 1) * rgb
    g = 0.55 * blur_rgb(hi, 5) + 0.5 * blur_rgb(hi, 18) + 0.45 * blur_rgb(hi, 60) + 0.35 * blur_rgb(hi, 160)
    return np.clip(rgb + gain * g, 0, 1)


def add_to_rgba(rgb):
    """검정 배경 더하기 RGB → 투명 PNG(alpha=max, 색=rgb/alpha)."""
    np, Image, _ = _post_imports()
    a = np.clip(rgb.max(axis=2) * 1.0, 0, 1)
    col = np.where(a[..., None] > 1e-4, rgb / np.maximum(a[..., None], 1e-4), 0)
    out = np.dstack([np.clip(col, 0, 1), a])
    return Image.fromarray(dither8(out))


GLOW = {'fire': 0.35, 'rainbow': 0.4, 'coins': 1.0, 'slime': 0.8}


def post(effect, glow=None):
    glow = GLOW.get(effect, 1.0) if glow is None else glow
    np, Image, _ = _post_imports()
    d = os.path.join(OUT, effect)
    meta = json.load(open(os.path.join(d, 'meta.json')))
    os.makedirs(os.path.join(d, 'add'), exist_ok=True)
    os.makedirs(os.path.join(d, 'rgba'), exist_ok=True)
    for f in range(1, meta['frames'] + 1):
        fa = os.path.join(d, 'raw', 'add_%04d.png' % f)
        if not os.path.exists(fa):
            continue
        add = np.asarray(Image.open(fa).convert('RGB')).astype('float32') / 255.0
        ft = os.path.join(d, 'raw', 'txt_%04d.png' % f)
        if os.path.exists(ft):
            t = np.asarray(Image.open(ft).convert('RGBA')).astype('float32') / 255.0
            ta = t[..., 3:4]
            tp = t[..., :3] * ta           # 글자: 투명 배경 렌더라 straight → premult
            # 글자 가장자리 번짐: 글자 밝기 일부도 같이 번지게 한 뒤 더하기 층에 합침
            comp = tp + add * 1.0          # 더하기 층은 글자 위에 더해짐
            glo = bloom(np.clip(comp, 0, 1), glow * 0.45)
            premult = glo
            alpha = np.clip(np.maximum(ta[..., 0], premult.max(axis=2)), 0, 1)
            col = np.where(alpha[..., None] > 1e-4, premult / np.maximum(alpha[..., None], 1e-4), 0)
            rgba = Image.fromarray(dither8(np.dstack([np.clip(col, 0, 1), alpha])))
            on_black = Image.fromarray(dither8(np.clip(premult, 0, 1)))
        else:
            glo = bloom(add, glow)
            rgba = add_to_rgba(glo)
            on_black = Image.fromarray(dither8(glo))
        rgba.save(os.path.join(d, 'rgba', 'f_%04d.png' % f))
        on_black.save(os.path.join(d, 'add', 'f_%04d.png' % f))
        print(effect, f, end='\r')


def sheet(effects, bg_path=None, frames_per=6, out=None):
    """효과별 대표 프레임 몇 장을 검정(더하기)·영상 캡처(일반 합성) 위에 얹은 한 장 시트."""
    np, Image, _ = _post_imports()
    from PIL import ImageDraw
    bg = Image.open(bg_path).convert('RGB').resize((W, H)) if bg_path else None
    tw, th = 480, 270
    rows = []
    for e in effects:
        d = os.path.join(OUT, e)
        meta = json.load(open(os.path.join(d, 'meta.json')))
        n = meta['frames']
        picks = sorted(set(int(1 + i * (n - 1) / (frames_per - 1)) for i in range(frames_per)))
        row = Image.new('RGB', (tw * frames_per, th * 2 + 18), (20, 20, 24))
        dr = ImageDraw.Draw(row)
        dr.text((6, 3), e, fill=(255, 255, 255))
        for i, f in enumerate(picks):
            k = os.path.join(d, 'add', 'f_%04d.png' % f)
            r = os.path.join(d, 'rgba', 'f_%04d.png' % f)
            if not os.path.exists(k):
                continue
            row.paste(Image.open(k).resize((tw, th), Image.LANCZOS), (i * tw, 18))
            if bg is not None:
                if 'txt' in meta.get('passes', []):      # 금속 글자·코인·방울: 일반 알파 합성
                    comp = bg.copy().convert('RGBA')
                    comp.alpha_composite(Image.open(r).convert('RGBA'))
                    comp = comp.convert('RGB')
                else:                                    # 더하기 이펙트: 영상에 rgb 를 더함
                    a = np.asarray(bg).astype('float32') / 255.0
                    b = np.asarray(Image.open(k).convert('RGB')).astype('float32') / 255.0
                    comp = Image.fromarray((np.clip(a + b, 0, 1) * 255).astype('uint8'))
                row.paste(comp.resize((tw, th), Image.LANCZOS), (i * tw, 18 + th))
            dr.text((i * tw + 4, 20), 'f%d' % f, fill=(200, 200, 200))
        rows.append(row)
    sh = Image.new('RGB', (rows[0].width, sum(r.height for r in rows)))
    y = 0
    for r in rows:
        sh.paste(r, (0, y))
        y += r.height
    out = out or os.path.join(OUT, 'sheet_' + '_'.join(effects)[:60] + '.png')
    sh.save(out)
    return out


if __name__ == '__main__':
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:]
    if not argv:
        sys.exit(__doc__)
    cmd = argv[0]
    if cmd == 'render' and HAVE_BPY:
        e = argv[1]
        lim = int(argv[2]) if len(argv) > 2 else None
        render_range(e, 1, lim)
    elif cmd == 'post':
        post(argv[1])
    elif cmd == 'sheet':
        bgp = None
        es = argv[1:]
        if es and es[0].startswith('--bg='):
            bgp = es[0][5:]
            es = es[1:]
        print(sheet(es, bgp))
