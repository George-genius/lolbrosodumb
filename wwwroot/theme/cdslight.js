ace.define('ace/theme/cdslight', [
    'require', 'exports', 'module',
    'ace/lib/dom'
], function(require, exports, module) {
    var dom = require('ace/lib/dom');

    exports.isDark = false;
    exports.cssClass = 'ace-cds-light';
    exports.cssText = `
.ace-cds-light {
    color: #111827;
    background-color: #f5f5f7;
}

/* 滚动条边上的 gutter 行号 */
.ace-cds-light .ace_gutter {
    background: #e5e7eb;
    color: #9ca3af;
}
.ace-cds-light .ace_gutter-active-line {
    background-color: #d4d4dd;
}

/* 普通文字、光标、选中、当前行 */
.ace-cds-light .ace_print-margin {
    width: 1px;
    background: #e5e7eb;
}
.ace-cds-light .ace_cursor {
    color: #111827;
}
.ace-cds-light .ace_marker-layer .ace_selection {
    background: rgba(148, 163, 184, 0.35);
}
.ace-cds-light .ace_marker-layer .ace_active-line {
    background: #e5e7eb;
}
.ace-cds-light .ace_marker-layer .ace_selected-word {
    border: 1px solid rgba(129, 140, 248, 0.75);
}

/* 括号匹配 */
.ace-cds-light .ace_marker-layer .ace_bracket {
    margin: -1px 0 0 -1px;
    border: 1px solid rgba(148, 163, 184, 0.8);
}

/* 注释 */
.ace-cds-light .ace_comment {
    color: #9ca3af;
    font-style: italic;
}
/* TODO / FIXME 可以配更亮一点（如果你以后在 rules 里单独打 token） */
.ace-cds-light .ace_keyword.ace_todo {
    color: #dc2626;
    font-weight: bold;
}

/* 关键字 */
.ace-cds-light .ace_keyword {
    color: #2563eb;
    font-weight: 600;
}

/* 布尔 / 特殊常量 (true / false / null / on / off / yes / no) */
.ace-cds-light .ace_constant.ace_language {
    color: #0891b2;
    font-weight: 500;
}

/* 数字：十六进制 / 二进制 / 浮点统统一类 */
.ace-cds-light .ace_constant.ace_numeric {
    color: #7c3aed;
}

/* 字符串 */
.ace-cds-light .ace_string {
    color: #047857;
}

/* section 名字: [main] / [script.core] */
.ace-cds-light .ace_entity.ace_name.ace_section {
    color: #d97706;
    font-weight: 600;
}

/* key = value 里的 key */
.ace-cds-light .ace_variable.ace_parameter {
    color: #0d9488;
    font-weight: 500;
}

/* path: value 这种类型名 / key 前缀 */
.ace-cds-light .ace_support.ace_type {
    color: #2563eb;
}

/* @directive 之类（走 keyword.control） */
.ace-cds-light .ace_keyword.ace_control {
    color: #7c3aed;
}

/* 运算符 / 标点 / 括号 */
.ace-cds-light .ace_keyword.ace_operator {
    color: #4b5563;
}
.ace-cds-light .ace_punctuation {
    color: #6b7280;
}
.ace-cds-light .ace_paren {
    color: #4b5563;
}

/* 折叠小三角 */
.ace-cds-light .ace_fold {
    background-color: #2563eb;
    border-color: #f5f5f7;
}

/* 禁用的文字（如果你以后要用） */
.ace-cds-light .ace_invisible {
    color: #d1d5db;
}
`;

    dom.importCssString(exports.cssText, exports.cssClass);
});
